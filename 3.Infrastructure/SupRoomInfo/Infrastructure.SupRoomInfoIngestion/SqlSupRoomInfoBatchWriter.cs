namespace Infrastructure.SupRoomInfoIngestion;

public sealed class SqlSupRoomInfoBatchWriter : ISupRoomInfoBatchWriter
{
    readonly IServiceScopeFactory _scopeFactory;
    readonly SupRoomInfoIngestionMetrics _metrics;
    readonly ILogger<SqlSupRoomInfoBatchWriter> _logger;

    public SqlSupRoomInfoBatchWriter(
        IServiceScopeFactory scopeFactory,
        SupRoomInfoIngestionMetrics metrics,
        ILogger<SqlSupRoomInfoBatchWriter> logger)
    {
        _scopeFactory = scopeFactory;
        _metrics = metrics;
        _logger = logger;
    }

    public async Task<int> WriteAsync(IReadOnlyList<TempSupRoomInfo2> batch, CancellationToken cancellationToken)
    {
        using var serviceScope = _scopeFactory.CreateScope();
        var dbContext = serviceScope.ServiceProvider.GetRequiredService<RoomMappingDbContext>();

        try
        {
            using (_metrics.MeasureDuration("temp_bulk_insert_duration"))
            {
                await dbContext.BulkInsertAsync(batch.ToList(), cancellationToken: cancellationToken);
            }

            _metrics.RecordPipelineStep("temp_bulk_insert", batch.Count);
        }
        catch (Exception ex)
        {
            _metrics.RecordError("temp_bulk_insert");
            _logger.LogError(ex, "批量写入 TempSupRoomInfo2 失败。count={Count}", batch.Count);
            throw;
        }

        try
        {
            using (_metrics.MeasureDuration("sync_stored_procedure_duration"))
            {
                await dbContext.Database.ExecuteSqlRawAsync("EXEC SyncSupRoomInfoFromTemp2", cancellationToken);
            }

            _metrics.RecordPipelineStep("sync_stored_procedure", batch.Count);
        }
        catch (Exception ex)
        {
            _metrics.RecordError("sync_stored_procedure");
            _logger.LogError(ex, "执行 SyncSupRoomInfoFromTemp2 失败。count={Count}", batch.Count);
            throw;
        }

        try
        {
            using (_metrics.MeasureDuration("temp_table_cleanup_duration"))
            {
                await dbContext.Database.ExecuteSqlRawAsync("TRUNCATE TABLE TempSupRoomInfo2", cancellationToken);
            }

            _metrics.RecordPipelineStep("temp_table_cleanup", batch.Count);
        }
        catch (Exception ex)
        {
            _metrics.RecordError("temp_table_cleanup");
            _logger.LogError(ex, "清理 TempSupRoomInfo2 失败。count={Count}", batch.Count);
            throw;
        }

        return batch.Count;
    }
}
