using System.Collections.Concurrent;
using System.IO.Compression;
using System.Text;
using System.Text.Json;
using Infrastructure.Sql.Admin;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using TaskScheduling.App;

namespace TaskScheduling.Infra;

internal sealed record ScheduledRunLogContext(Guid RunId, string RelativePath, TaskLogFilter? LogFilter);

internal sealed class ScheduledRunContextAccessor
{
    private readonly AsyncLocal<ScheduledRunLogContext?> _current = new();
    public ScheduledRunLogContext? Current => _current.Value;

    public IDisposable Push(Guid runId, string relativePath, TaskLogFilter? logFilter = null)
    {
        var previous = _current.Value;
        _current.Value = new ScheduledRunLogContext(runId, relativePath, logFilter);
        return new PopWhenDisposed(() => _current.Value = previous);
    }

    private sealed class PopWhenDisposed(Action pop) : IDisposable
    {
        private int _disposed;
        public void Dispose()
        {
            if (Interlocked.Exchange(ref _disposed, 1) == 0) pop();
        }
    }
}

internal sealed record RunLogLine(
    long Sequence,
    DateTime TimestampUtc,
    string Level,
    string Category,
    int EventId,
    string Message,
    IReadOnlyDictionary<string, object?> Properties,
    string? Exception);

internal sealed class RunFileLogStore(IOptions<TaskSchedulingOptions> options)
{
    private readonly string _root = PrepareRoot(options.Value.LogDirectory);
    private readonly long _maxRunBytes = Math.Max(1024, options.Value.MaxRunLogBytes);
    private readonly ConcurrentDictionary<Guid, object> _locks = new();
    private readonly ConcurrentDictionary<Guid, long> _sequences = new();

    public void Write(ScheduledRunLogContext context, LogLevel level, string category, EventId eventId,
        string message, IReadOnlyDictionary<string, object?> properties, Exception? exception)
    {
        try
        {
            var path = Resolve(context.RelativePath);
            var gate = _locks.GetOrAdd(context.RunId, _ => new object());
            lock (gate)
            {
                Directory.CreateDirectory(Path.GetDirectoryName(path)!);
                if (File.Exists(path) && new FileInfo(path).Length >= _maxRunBytes) return;
                var sequence = _sequences.AddOrUpdate(context.RunId, 1, (_, current) => current + 1);
                var line = new RunLogLine(sequence, DateTime.UtcNow, level.ToString(), category,
                    eventId.Id, message, properties, exception?.ToString());
                File.AppendAllText(path, JsonSerializer.Serialize(line, JsonDefaults.Options) + Environment.NewLine, new UTF8Encoding(false));
            }
        }
        catch
        {
            // Logging must never fail the scheduled task.
        }
    }

    public string Resolve(string relativePath)
    {
        var path = Path.GetFullPath(Path.Combine(_root, relativePath));
        var rootPrefix = _root.TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;
        if (!path.StartsWith(rootPrefix, StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("Scheduled log path is outside the configured log directory.");
        return path;
    }

    public (long Length, bool Truncated) GetFileState(string relativePath)
    {
        var path = Resolve(relativePath);
        if (!File.Exists(path)) return (0, false);
        var length = new FileInfo(path).Length;
        return (length, length >= _maxRunBytes);
    }

    public async Task CompressAsync(string relativePath)
    {
        var path = Resolve(relativePath);
        if (!File.Exists(path) || path.EndsWith(".gz", StringComparison.OrdinalIgnoreCase)) return;
        var compressed = path + ".gz";
        await using var source = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read);
        await using var target = new FileStream(compressed, FileMode.Create, FileAccess.Write);
        await using var gzip = new GZipStream(target, CompressionLevel.SmallestSize);
        await source.CopyToAsync(gzip);
        File.Delete(path);
    }

    public string Root => _root;

    private static string PrepareRoot(string value)
    {
        var root = Path.GetFullPath(value);
        Directory.CreateDirectory(root);
        return root;
    }
}

internal sealed class ScheduledRunLoggerProvider(
    ScheduledRunContextAccessor contextAccessor,
    RunFileLogStore store) : ILoggerProvider
{
    public ILogger CreateLogger(string categoryName) => new Logger(categoryName, contextAccessor, store);
    public void Dispose() { }

    private sealed class Logger(string category, ScheduledRunContextAccessor accessor, RunFileLogStore store) : ILogger
    {
        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

        public bool IsEnabled(LogLevel logLevel)
        {
            if (logLevel < LogLevel.Information) return false;
            var filter = accessor.Current?.LogFilter;
            if (filter is null) return true;
            var minLevel = ResolveMinLevel(category, filter);
            return logLevel >= minLevel;
        }

        private static LogLevel ResolveMinLevel(string category, TaskLogFilter filter)
        {
            foreach (var rule in filter.Rules)
            {
                if (category.StartsWith(rule.CategoryPrefix, StringComparison.Ordinal)
                    && Enum.TryParse<LogLevel>(rule.MinLevel, true, out var level))
                    return level;
            }
            return LogLevel.Information;
        }

        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception,
            Func<TState, Exception?, string> formatter)
        {
            var context = accessor.Current;
            if (context is null || !IsEnabled(logLevel)) return;
            var properties = state is IEnumerable<KeyValuePair<string, object?>> pairs
                ? pairs.Where(x => x.Key != "{OriginalFormat}").ToDictionary(x => x.Key, x => SafeValue(x.Value))
                : new Dictionary<string, object?>();
            store.Write(context, logLevel, category, eventId, formatter(state, exception), properties, exception);
        }

        private static object? SafeValue(object? value) => value switch
        {
            null or string or bool or byte or short or int or long or float or double or decimal or Guid or DateTime or DateTimeOffset => value,
            _ => value.ToString()
        };
    }
}

internal sealed class ScheduledTaskLogReader(
    AdminDbContext dbContext,
    RunFileLogStore store) : IScheduledTaskLogReader
{
    public async Task<ScheduledTaskLogPage> ReadAsync(Guid runId, long afterSequence, int take, CancellationToken cancellationToken)
    {
        var run = await dbContext.ScheduledTaskRuns.AsNoTracking().SingleOrDefaultAsync(x => x.Id == runId, cancellationToken);
        if (run is null) throw new KeyNotFoundException("Run was not found.");
        if (run.LogDeletedAtUtc.HasValue) return new ScheduledTaskLogPage(runId, [], afterSequence, true, false);
        var path = store.Resolve(run.LogRelativePath);
        if (!File.Exists(path)) return new ScheduledTaskLogPage(runId, [], afterSequence, IsComplete(run.Status), false);

        var items = new List<ScheduledTaskLogEntry>();
        var isCompressed = path.EndsWith(".gz", StringComparison.OrdinalIgnoreCase);
        await using var fileStream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read);
        await using var decompress = isCompressed ? new GZipStream(fileStream, CompressionMode.Decompress) : null;
        using var reader = new StreamReader((Stream?)decompress ?? fileStream, Encoding.UTF8);
        while (items.Count < take && await reader.ReadLineAsync(cancellationToken) is { } line)
        {
            RunLogLine? entry;
            try { entry = JsonSerializer.Deserialize<RunLogLine>(line, JsonDefaults.Options); }
            catch (JsonException) { continue; }
            if (entry is null || entry.Sequence <= afterSequence) continue;
            items.Add(new ScheduledTaskLogEntry(entry.Sequence, entry.TimestampUtc, entry.Level,
                entry.Category, entry.EventId, entry.Message, entry.Properties, entry.Exception));
        }
        var next = items.Count == 0 ? afterSequence : items[^1].Sequence;
        return new ScheduledTaskLogPage(runId, items, next, IsComplete(run.Status), true);
    }

    private static bool IsComplete(string status) => status is not (ScheduledTaskStatuses.Queued or ScheduledTaskStatuses.Running);
}
