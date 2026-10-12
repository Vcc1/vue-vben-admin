using OTAHotelInfoSync.Models;

namespace OTAHotelInfoSync.Contracts;

public interface IOTAHotelInfoRepository
{
    Task<bool> HasAnyAsync(CancellationToken cancellationToken);

    Task<DateTime?> GetLastModifyTimeAsync(CancellationToken cancellationToken);

    Task<int> UpsertAsync(
        IReadOnlyList<OTAHotelInfoRecord> hotels,
        CancellationToken cancellationToken);
}
