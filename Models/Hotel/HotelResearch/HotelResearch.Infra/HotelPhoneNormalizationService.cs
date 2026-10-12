using HotelMatchingPipeline.Domain.Hotels;
using HotelResearch.App;
using HotelResearch.Contracts;

namespace HotelResearch.Infra;

internal sealed class HotelPhoneNormalizationService(
    IHotelPhoneNormalizer phoneNormalizer) : IHotelPhoneNormalizationService
{
    public HotelPhoneNormalizationBatchResult Normalize(HotelPhoneNormalizationBatchRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);

        var cache = new Dictionary<(string Raw, string CountryCode), IReadOnlyList<HotelPhoneNormalizationValue>>();
        var items = request.Items.Select(item =>
        {
            var raw = item.Raw.Trim();
            var countryCode = item.CountryCode?.Trim() ?? string.Empty;
            var cacheKey = (raw, countryCode.ToUpperInvariant());
            if (!cache.TryGetValue(cacheKey, out var values))
            {
                values = phoneNormalizer
                    .Normalize(raw, RegionResolution.NotFound(), countryCode)
                    .Select(phone => new HotelPhoneNormalizationValue
                    {
                        Raw = phone.Raw,
                        E164 = phone.E164,
                        NationalNumber = phone.NationalNumber,
                        CountryCallingCode = phone.CountryCallingCode,
                        AreaCode = phone.AreaCode,
                        Extension = phone.Extension,
                        RegionCode = phone.RegionCode,
                        NumberType = phone.NumberType,
                        IsValid = phone.IsValid,
                        Error = phone.Error
                    })
                    .ToArray();
                cache[cacheKey] = values;
            }

            return new HotelPhoneNormalizationResultItem
            {
                Id = item.Id.Trim(),
                Input = raw,
                Values = values
            };
        }).ToArray();

        return new HotelPhoneNormalizationBatchResult { Items = items };
    }
}
