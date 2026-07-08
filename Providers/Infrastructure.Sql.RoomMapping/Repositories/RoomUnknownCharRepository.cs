using Framework.Core.IOC;
using Infrastructure.Sql.RoomMapping;
using Infrastructure.Sql.RoomMapping.Entitys;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using RoomMatch.Core.RoomAnalysis;
using System.Collections.Concurrent;
using System.Data;

namespace Infrastructure.Sql.RoomMapping.Repositories
{
    [Service(Microsoft.Extensions.DependencyInjection.ServiceLifetime.Singleton)]
    public class RoomUnknownCharRepository : IRoomUnknownCharRepository, IAsyncDisposable
    {
        private readonly CancellationTokenSource cts;
        private readonly CancellationToken ct;
        private readonly IServiceProvider _serviceProvider;

        public RoomUnknownCharRepository(IServiceProvider serviceProvider)
        {
            _serviceProvider = serviceProvider;
            cts = new CancellationTokenSource();
            ct = cts.Token;
            _ = ExecuteAsync();
        }

        private readonly ConcurrentDictionary<int, byte> _localSeen = new();
        private readonly ConcurrentDictionary<int, byte> _dbCache = new();
        private int _dirty;
        private readonly TimeSpan _flushInterval = TimeSpan.FromSeconds(60);

        public void Report(System.Text.Rune r)
        {
            if (r.Value < 128)
            {
                return;
            }

            if (_localSeen.TryAdd(r.Value, 0))
            {
                Interlocked.Exchange(ref _dirty, 1);
            }
        }

        public void Report(char ch)
        {
            if (ch < 128)
            {
                return;
            }

            if (_localSeen.TryAdd(ch, 0))
            {
                Interlocked.Exchange(ref _dirty, 1);
            }
        }

        public int GetLocalSeenCount()
        {
            return _localSeen.Count;
        }

        public int GetDbCacheCount()
        {
            return _dbCache.Count;
        }

        protected async Task ExecuteAsync()
        {
            await PreloadDbCacheAsync();
            while (!ct.IsCancellationRequested)
            {
                try
                {
                    await Task.Delay(_flushInterval, ct);
                    await FlushAsync();
                }
                catch (OperationCanceledException)
                {
                }
                catch (Exception)
                {
                }
            }
        }

        public async Task FlushAsync()
        {
            if (Interlocked.Exchange(ref _dirty, 0) == 0)
            {
                return;
            }

            var candidates = new List<int>(capacity: Math.Max(1024, _localSeen.Count));
            foreach (var kv in _localSeen)
            {
                if (!_dbCache.ContainsKey(kv.Key))
                {
                    candidates.Add(kv.Key);
                }
            }

            if (candidates.Count == 0)
            {
                return;
            }

            const int batch = 1000;
            for (int i = 0; i < candidates.Count; i += batch)
            {
                var slice = candidates.Skip(i).Take(batch).ToArray();
                await BulkInsertOnceAsync(slice, ct);

                foreach (var cp in slice)
                {
                    _dbCache.TryAdd(cp, 0);
                }
            }
        }

        private async Task PreloadDbCacheAsync()
        {
            await using var db = _serviceProvider.CreateScope().ServiceProvider.GetRequiredService<RoomMappingDbContext>();
            var cps = await db.Set<NormUnknownChar>()
                .Select(x => x.CodePoint)
                .ToListAsync(ct);

            foreach (var cp in cps)
            {
                _dbCache.TryAdd(cp, 0);
            }
        }

        private async Task BulkInsertOnceAsync(int[] cps, CancellationToken ct)
        {
            if (cps.Length == 0)
            {
                return;
            }

            var tvp = new DataTable();
            tvp.Columns.Add("CodePoint", typeof(int));
            tvp.Columns.Add("Glyph", typeof(string));
            tvp.Columns.Add("Category", typeof(string));

            foreach (var cp in cps)
            {
                var rune = new System.Text.Rune(cp);
                tvp.Rows.Add(cp, rune.ToString(), System.Text.Rune.GetUnicodeCategory(rune).ToString());
            }

            await using var db = _serviceProvider.CreateScope().ServiceProvider.GetRequiredService<RoomMappingDbContext>();
            var conn = (SqlConnection)db.Database.GetDbConnection();
            var wasOpen = conn.State == ConnectionState.Open;
            if (!wasOpen)
            {
                await conn.OpenAsync(ct);
            }

            try
            {
                using var cmd = new SqlCommand("dbo.NormUnknownChar_BulkInsertOnce", conn)
                {
                    CommandType = CommandType.StoredProcedure
                };
                var parameter = cmd.Parameters.Add("@New", SqlDbType.Structured);
                parameter.TypeName = "dbo.NormUnknownCharList";
                parameter.Value = tvp;

                await cmd.ExecuteNonQueryAsync(ct);
            }
            finally
            {
                if (!wasOpen)
                {
                    await conn.CloseAsync();
                }
            }
        }

        public async ValueTask DisposeAsync()
        {
            await cts.CancelAsync();
        }
    }
}
