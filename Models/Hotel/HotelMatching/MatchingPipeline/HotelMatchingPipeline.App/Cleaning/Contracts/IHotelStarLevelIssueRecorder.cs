using HotelMatchingPipeline.Domain.Hotels;

namespace HotelMatchingPipeline.App.Contracts;

public interface IHotelStarLevelIssueRecorder
{
    void Report(HotelDataType dataType, int? supplierId, int? sourceId, string? rawValue, string reason);

    Task FlushAsync(CancellationToken cancellationToken = default);
}
