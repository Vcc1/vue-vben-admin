using Infrastructure.HTTP.OTAHotelInfo.Models;

namespace Infrastructure.HTTP.OTAHotelInfo;

public interface IOtaHotelInfoClient
{
    Task<PageResult<BaseHotelInfo>> QueryByMinHotelIdAsync(
        int minHotelId,
        int pageSize,
        CancellationToken cancellationToken = default);

    Task<PageResult<BaseHotelInfo>> QueryByMinHotelIdPageAsync(
        int minHotelId,
        int pageSize,
        int pageIndex,
        CancellationToken cancellationToken = default);

    Task<PageResult<BaseHotelInfo>> QueryByTimeAsync(
        DateTime startTime,
        DateTime? endTime = null,
        CancellationToken cancellationToken = default);
}
