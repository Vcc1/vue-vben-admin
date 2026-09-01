using HotelResearch.App;
using HotelResearch.Contracts;
using Infrastructure.Sql.RoomMapping;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;

namespace HotelResearch.Infra;

internal sealed class RoomMappingHotelResearchSeedReader(
    IConfiguration configuration) : IHotelResearchSeedReader
{
    const int MaximumRoomCount = 200;

    public async Task<HotelResearchDatabaseHotel?> FindAsync(
        string mtHotelCode,
        CancellationToken cancellationToken = default)
    {
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

        var hotel = await database.OTAHotelInfos
            .AsNoTracking()
            .Include(item => item.Phones)
            .Where(item => item.MTHotelCode == mtHotelCode)
            .OrderBy(item => item.HotelID)
            .FirstOrDefaultAsync(cancellationToken);
        if (hotel is null)
        {
            return null;
        }

        var rooms = await database.OTARoomInfos
            .AsNoTracking()
            .Include(item => item.Extension)
            .Where(item => item.MTHotelCode == mtHotelCode)
            .OrderBy(item => item.Id)
            .Take(MaximumRoomCount)
            .ToArrayAsync(cancellationToken);

        var phones = hotel.Phones
            .OrderBy(phone => phone.Ordinal)
            .Select(phone => phone.IsValid
                ? FirstValue(phone.NormalizedPhone, phone.NationalNumber, phone.SourcePhone)
                : NullIfEmpty(phone.SourcePhone))
            .Where(phone => phone is not null)
            .Cast<string>()
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
        if (phones.Count == 0 && !string.IsNullOrWhiteSpace(hotel.Tel))
        {
            phones.Add(hotel.Tel.Trim());
        }

        return new HotelResearchDatabaseHotel
        {
            HotelId = hotel.HotelID,
            MTHotelCode = hotel.MTHotelCode,
            HotelName = hotel.HotelName,
            HotelNameEnglish = hotel.HotelEnName,
            Address = hotel.Address,
            AddressEnglish = hotel.EnAddress,
            CountryCode = hotel.CountryCode,
            CountryName = hotel.CountryName,
            CountryNameEnglish = hotel.CountryNameEn,
            CityName = hotel.CityName,
            CityNameEnglish = hotel.CityNameEn,
            Longitude = hotel.Longitude,
            Latitude = hotel.Latitude,
            StarRating = hotel.StarLevel,
            OfficialWebsite = hotel.WebSite,
            HotelPhones = phones,
            Rooms = rooms.Select(room => new HotelResearchDatabaseRoom
            {
                RoomId = room.Id,
                RoomNameChinese = NullIfEmpty(room.RoomNameCN),
                RoomNameEnglish = NullIfEmpty(room.RoomNameEN),
                Area = FirstValue(
                    room.Extension?.ExternalArea,
                    room.Extension?.InternalArea,
                    FormatArea(room.AreaMin, room.AreaMax)),
                BedType = FirstValue(
                    room.Extension?.ExternalBedInfo,
                    room.Extension?.InternalBedInfo,
                    room.BedInfo),
                IsNonSmoking = null,
                View = FirstValue(room.Extension?.ExternalView, room.Extension?.InternalView)
            }).ToArray()
        };
    }

    static string? FormatArea(short? minimum, short? maximum)
    {
        if (minimum is null or <= 0 && maximum is null or <= 0)
        {
            return null;
        }

        if (minimum is > 0 && maximum is > 0 && minimum != maximum)
        {
            return $"{minimum}-{maximum} m²";
        }

        return $"{(minimum is > 0 ? minimum : maximum)} m²";
    }

    static string? FirstValue(params string?[] values)
        => values.Select(NullIfEmpty).FirstOrDefault(value => value is not null);

    static string? NullIfEmpty(string? value)
        => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
