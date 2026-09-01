using Microsoft.Extensions.Logging;
using Microsoft.Extensions.DependencyInjection;

namespace OTARoomBuilder;

public sealed partial class OTARoomBuilderWorker
{
    async Task RunMatcherAsync(CancellationToken cancellationToken)
    {
        while (await _hotelBuildChannel.Reader.WaitToReadAsync(cancellationToken))
        {
            while (_hotelBuildChannel.Reader.TryRead(out var item))
            {
                _metrics.RecordQueueDequeued("hotel_build");
                await RunMatchStageSafelyAsync(item, cancellationToken);
            }
        }
    }

    async Task RunMatchStageSafelyAsync(HotelBuildQueueItem item, CancellationToken cancellationToken)
    {
        var isHotel = item.Hotel is not null;
        if (isHotel)
        {
            _metrics.RecordStageStarted("match");
        }

        try
        {
            if (item.Hotel is null)
            {
                if (item.Completion is not null)
                {
                    await _staticDataChannel.Writer.WriteAsync(StaticDataQueueItem.ForCompletion(item.Completion), cancellationToken);
                    _metrics.RecordQueueEnqueued("static_data");
                }
                return;
            }

            using var scope = _scopeFactory.CreateScope();
            var processor = scope.ServiceProvider.GetRequiredService<OTARoomBuilderProcessor>();

            cancellationToken.ThrowIfCancellationRequested();
            var matchResult = await processor.MatchOtaRoomsForHotelAsync(item.Hotel, cancellationToken);
            await _staticDataChannel.Writer.WriteAsync(
                StaticDataQueueItem.ForHotel(item.Hotel, matchResult, item.OutputMode ?? string.Empty),
                cancellationToken);
            _metrics.RecordQueueEnqueued("static_data");
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            item.Completion?.TrySetCanceled(cancellationToken);
            throw;
        }
        catch (Exception ex)
        {
            item.Completion?.TrySetException(ex);
            if (item.Hotel is not null)
            {
                await _staticDataChannel.Writer.WriteAsync(
                    StaticDataQueueItem.ForHotel(
                        item.Hotel,
                        HotelRoomMatchResult.Empty(item.Hotel.MTHotelCode),
                        item.OutputMode ?? string.Empty),
                    cancellationToken);
                _metrics.RecordQueueEnqueued("static_data");
            }
            _metrics.RecordError("match_stage");
            _logger.LogError(ex, "OTARoomBuilder match stage failed");
        }
        finally
        {
            if (isHotel)
            {
                _metrics.RecordStageCompleted("match");
                _metrics.RecordStageFinished("match");
            }
        }
    }
}
