using HotelResearch.Contracts;

namespace HotelResearch.App;

public interface IHotelResearchSeedReader
{
    Task<HotelResearchDatabaseHotel?> FindAsync(
        string mtHotelCode,
        CancellationToken cancellationToken = default);
}

public interface IHotelBusinessDataReader
{
    Task<HotelBusinessDataSnapshot> ReadAsync(
        string mtHotelCode,
        CancellationToken cancellationToken = default);
}

public interface IHotelPhoneNormalizationService
{
    HotelPhoneNormalizationBatchResult Normalize(HotelPhoneNormalizationBatchRequest request);
}
