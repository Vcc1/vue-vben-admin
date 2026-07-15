using OTAHotelInfoSync.Models;

namespace OTAHotelInfoSync.Contracts;

public interface IOTAHotelInfoSource
{
    Task<IReadOnlyList<OTAHotelInfoRecord>> QueryByMinHotelIdAsync(
        int minHotelId,
        int pageSize,
        CancellationToken cancellationToken);

    Task<IReadOnlyList<OTAHotelInfoRecord>> QueryByTimeAsync(
        DateTime startTime,
        DateTime? endTime,
        CancellationToken cancellationToken);
}
