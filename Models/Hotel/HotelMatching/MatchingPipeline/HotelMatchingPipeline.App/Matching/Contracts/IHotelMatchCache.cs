using HotelMatchingPipeline.Domain.Hotels;

namespace HotelMatchingPipeline.App.Contracts;

public interface IHotelMatchCache
{
    Task SaveAsync(
        IReadOnlyCollection<HotelMatchEvaluation> evaluations,
        int matchingVersion,
        CancellationToken cancellationToken);
}
