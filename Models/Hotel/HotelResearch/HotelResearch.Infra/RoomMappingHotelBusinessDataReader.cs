using HotelBusiness.App;
using HotelResearch.App;
using HotelResearch.Contracts;
using Infrastructure.Sql.RoomMapping;
using Infrastructure.Sql.RoomMapping.Entitys;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;

namespace HotelResearch.Infra;

internal sealed class RoomMappingHotelBusinessDataReader(
    IConfiguration configuration) : IHotelBusinessDataReader
{
    public async Task<HotelBusinessDataSnapshot> ReadAsync(
        string mtHotelCode,
        CancellationToken cancellationToken = default)
    {
        var normalizedCode = HotelBusinessService.NormalizeHotelCode(mtHotelCode);
        var connectionString = configuration.GetConnectionString("RoomMapping");
        if (string.IsNullOrWhiteSpace(connectionString))
        {
            throw new HotelResearchConfigurationException(
                "RoomMapping database is not configured. Set ConnectionStrings:RoomMapping.");
        }

        var options = new DbContextOptionsBuilder<RoomMappingDbContext>()
            .UseSqlServer(connectionString, sql => sql.CommandTimeout(30))
            .Options;
        await using var database = new RoomMappingDbContext(options);

        var databaseHotel = await database.OTAHotelInfos
            .AsNoTracking()
            .Include(hotel => hotel.Phones)
            .Where(hotel => hotel.MTHotelCode == normalizedCode)
            .OrderBy(hotel => hotel.HotelID)
            .FirstOrDefaultAsync(cancellationToken);

        var originalMatches = await database.SupHotelInfos
            .AsNoTracking()
            .AsSplitQuery()
            .Include(hotel => hotel.Phones)
            .Where(hotel => hotel.MTHotelCode == normalizedCode)
            .OrderBy(hotel => hotel.SupId)
            .ThenBy(hotel => hotel.SHotelCode)
            .ToArrayAsync(cancellationToken);

        var newMatches = await database.HotelMatchResults
            .AsNoTracking()
            .AsSplitQuery()
            .Include(result => result.SupplierHotel)
                .ThenInclude(hotel => hotel.Phones)
            .Where(result => result.MTHotelCode == normalizedCode)
            .OrderBy(result => result.Id)
            .ToArrayAsync(cancellationToken);

        return new HotelBusinessDataSnapshot
        {
            MTHotelCode = normalizedCode,
            DatabaseHotel = databaseHotel is null ? null : MapDatabaseHotel(databaseHotel),
            OriginalMatches = originalMatches.Select(hotel => MapHotel(hotel, null)).ToArray(),
            NewMatches = newMatches.Select(result => MapHotel(result.SupplierHotel, result)).ToArray()
        };
    }

    static HotelBusinessDatabaseHotelRecord MapDatabaseHotel(OTAHotelInfo hotel)
    {
        var phoneDetails = hotel.Phones
            .OrderBy(phone => phone.Ordinal)
            .Select(phone =>
            {
                var raw = NullIfEmpty(phone.SourcePhone);
                var normalized = phone.IsValid
                    ? FirstValue(phone.NormalizedPhone, phone.NationalNumber)
                    : null;
                var value = normalized ?? raw;
                return raw is null || value is null
                    ? null
                    : new HotelBusinessPhoneValue
                    {
                        Raw = raw,
                        Value = value,
                        IsNormalized = normalized is not null
                    };
            })
            .Where(phone => phone is not null)
            .Cast<HotelBusinessPhoneValue>()
            .DistinctBy(phone => (phone.Raw.ToUpperInvariant(), phone.Value.ToUpperInvariant()))
            .ToList();
        if (phoneDetails.Count == 0 && NullIfEmpty(hotel.Tel) is { } fallback)
        {
            phoneDetails.Add(new HotelBusinessPhoneValue
            {
                Raw = fallback,
                Value = fallback,
                IsNormalized = false
            });
        }

        return new HotelBusinessDatabaseHotelRecord
        {
            HotelId = hotel.HotelID,
            HotelName = NullIfEmpty(hotel.HotelName),
            HotelNameEnglish = NullIfEmpty(hotel.HotelEnName),
            CountryCode = NullIfEmpty(hotel.CountryCode),
            Phones = phoneDetails.Select(phone => phone.Value).Distinct(StringComparer.OrdinalIgnoreCase).ToArray(),
            PhoneDetails = phoneDetails,
            Address = NullIfEmpty(hotel.Address),
            AddressEnglish = NullIfEmpty(hotel.EnAddress),
            Longitude = hotel.Longitude,
            Latitude = hotel.Latitude,
            StarRating = hotel.StarLevel,
            UpdatedAt = hotel.UpdateTime
        };
    }

    static HotelBusinessDataRecord MapHotel(
        SupHotelInfoClean hotel,
        HotelMatchResult? match)
    {
        var phoneDetails = BuildPhoneDetails(hotel);
        return new()
        {
            SupHotelId = hotel.SupHotelId,
            SupId = hotel.SupId,
            SHotelCode = hotel.SHotelCode,
            HotelName = NullIfEmpty(hotel.HotelName),
            HotelNameEnglish = NullIfEmpty(hotel.HotelNameEn),
            CountryCode = NullIfEmpty(hotel.CountryCode),
            Phones = phoneDetails.Select(phone => phone.Value).Distinct(StringComparer.OrdinalIgnoreCase).ToArray(),
            PhoneDetails = phoneDetails,
            Address = NullIfEmpty(hotel.Address),
            Longitude = hotel.Longitude,
            Latitude = hotel.Latitude,
            StarRating = hotel.StarLevel,
            UpdatedAt = hotel.UpdateTime,
            MatchEvidence = match is null
                ? null
                : new HotelBusinessMatchEvidence
                {
                    MatchResultId = match.Id,
                    NamePendingValue = NullIfEmpty(match.NamePendingValue),
                    NameEvidenceValue = NullIfEmpty(match.NameEvidenceValue),
                    AddressPendingValue = NullIfEmpty(match.AddressPendingValue),
                    AddressEvidenceValue = NullIfEmpty(match.AddressEvidenceValue),
                    PhonePendingValue = NullIfEmpty(match.PhonePendingValue),
                    PhoneEvidenceValue = NullIfEmpty(match.PhoneEvidenceValue),
                    DistanceMeters = match.DistanceMeters,
                    MatchedAt = match.UpdateTime
                }
        };
    }

    static IReadOnlyList<HotelBusinessPhoneValue> BuildPhoneDetails(SupHotelInfoClean hotel)
    {
        var phones = hotel.Phones
            .OrderBy(phone => phone.Ordinal)
            .Select(phone =>
            {
                var raw = NullIfEmpty(phone.Raw);
                var normalized = phone.IsValid ? FirstValue(phone.E164, phone.NationalNumber) : null;
                var value = normalized ?? raw;
                return raw is null || value is null
                    ? null
                    : new HotelBusinessPhoneValue
                    {
                        Raw = raw,
                        Value = value,
                        IsNormalized = normalized is not null
                    };
            })
            .Where(phone => phone is not null)
            .Cast<HotelBusinessPhoneValue>()
            .DistinctBy(phone => (phone.Raw.ToUpperInvariant(), phone.Value.ToUpperInvariant()))
            .ToList();
        if (phones.Count == 0 && NullIfEmpty(hotel.Tel) is { } fallback)
        {
            phones.Add(new HotelBusinessPhoneValue
            {
                Raw = fallback,
                Value = fallback,
                IsNormalized = false
            });
        }

        return phones;
    }

    static string? FirstValue(params string?[] values)
        => values.Select(NullIfEmpty).FirstOrDefault(value => value is not null);

    static string? NullIfEmpty(string? value)
        => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
