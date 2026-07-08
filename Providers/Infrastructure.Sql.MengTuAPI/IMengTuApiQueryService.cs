using Infrastructures.Sql.MengTuAPI.Entitys;

namespace Infrastructure.Sql.MengTuAPI;

public interface IMengTuApiQueryService
{
    Task<IReadOnlyList<SupHotelInfo>> GetSupHotelInfosAsync(
        int supId,
        IReadOnlyCollection<string> mtHotelCodes,
        CancellationToken cancellationToken = default);
}
