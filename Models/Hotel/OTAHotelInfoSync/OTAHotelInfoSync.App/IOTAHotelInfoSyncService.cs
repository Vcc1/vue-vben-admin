namespace OTAHotelInfoSync;

public interface IOTAHotelInfoSyncService
{
    Task RunOnceAsync(CancellationToken cancellationToken);
}
