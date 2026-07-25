namespace HotelMatchingPipeline.App.Contracts;

public interface IHotelStarLevelConverter
{
    bool TryConvert(int? supplierId, string rawValue, out decimal? value);
}
