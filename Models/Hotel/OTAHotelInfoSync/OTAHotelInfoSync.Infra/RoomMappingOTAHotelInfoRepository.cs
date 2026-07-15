using Infrastructure.Sql.RoomMapping;
using Infrastructure.Sql.RoomMapping.Entitys;
using Microsoft.EntityFrameworkCore;
using OTAHotelInfoSync.Contracts;
using OTAHotelInfoSync.Models;

namespace Infrastructure.OTAHotelInfoSync;

public sealed class RoomMappingOTAHotelInfoRepository : IOTAHotelInfoRepository
{
    const int LocalWriteBatchSize = 1_000;

    readonly RoomMappingDbContext _dbContext;

    public RoomMappingOTAHotelInfoRepository(RoomMappingDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public Task<bool> HasAnyAsync(CancellationToken cancellationToken)
    {
        return _dbContext.OTAHotelInfoRaws
            .AsNoTracking()
            .AnyAsync(cancellationToken);
    }

    public Task<DateTime?> GetLastModifyTimeAsync(CancellationToken cancellationToken)
    {
        return _dbContext.OTAHotelInfoRaws
            .AsNoTracking()
            .Where(x => x.LastModifyTime.HasValue)
            .MaxAsync(x => x.LastModifyTime, cancellationToken);
    }

    public async Task<int> UpsertAsync(
        IReadOnlyList<OTAHotelInfoRecord> hotels,
        CancellationToken cancellationToken)
    {
        var incoming = hotels
            .Where(x => x.HotelID > 0)
            .GroupBy(x => x.HotelID)
            .Select(x => x.First())
            .ToArray();
        var total = 0;

        foreach (var batch in incoming.Chunk(LocalWriteBatchSize))
        {
            total += await UpsertBatchAsync(batch, cancellationToken);
        }

        return total;
    }

    async Task<int> UpsertBatchAsync(
        OTAHotelInfoRecord[] hotels,
        CancellationToken cancellationToken)
    {
        var hotelIds = hotels.Select(x => x.HotelID).ToArray();
        var existingRows = await _dbContext.OTAHotelInfoRaws
            .AsTracking()
            .Where(x => hotelIds.Contains(x.HotelID))
            .ToListAsync(cancellationToken);
        var existingByHotelId = existingRows
            .GroupBy(x => x.HotelID)
            .ToDictionary(x => x.Key, x => x.First());

        var now = DateTime.Now;
        foreach (var hotel in hotels)
        {
            if (!existingByHotelId.TryGetValue(hotel.HotelID, out var entity))
            {
                entity = new OTAHotelInfoRaw
                {
                    HotelID = hotel.HotelID,
                    CreateTime = now,
                    Flag = 0
                };
                _dbContext.OTAHotelInfoRaws.Add(entity);
            }

            MapHotel(entity, hotel);
            entity.LastSyncTime = now;
            entity.UpdateTime = now;
        }

        await _dbContext.SaveChangesAsync(cancellationToken);
        _dbContext.ChangeTracker.Clear();
        return hotels.Length;
    }

    static void MapHotel(OTAHotelInfoRaw target, OTAHotelInfoRecord source)
    {
        target.MTHotelCode = TrimToMax(
            string.IsNullOrWhiteSpace(source.HotelCode) ? $"MT-{source.HotelID}" : source.HotelCode,
            100)!;
        target.HotelName = TrimToMax(
            string.IsNullOrWhiteSpace(source.HotelName) ? target.MTHotelCode : source.HotelName,
            200)!;
        target.HotelEnName = TrimToMax(source.HotelEnName, 300);
        target.CityCode = TrimToMax(source.CityCode, 90);
        target.Address = TrimToMax(source.Address, 300);
        target.EnAddress = TrimToMax(source.EnAddress, 500);
        target.Estate = source.Estate;
        target.AreaID = 0;
        target.CountryID = source.CountryID;
        target.ProvinceID = source.ProvinceID;
        target.RootCityID = 0;
        target.CityID = source.CityID;
        target.CountryName = TrimToMax(source.CountryName, 64);
        target.CountryCode = TrimToMax(source.CountryCode, 128);
        target.CountryNameEn = TrimToMax(source.CountryNameEn, 100);
        target.CityName = TrimToMax(source.CityName, 64);
        target.CityNameEn = TrimToMax(source.CityNameEn, 100);
        target.Tel = TrimToMax(source.Tel, 60);
        target.Fax = TrimToMax(source.Fax, 60);
        target.PostCode = TrimToMax(source.PostCode, 50);
        target.Email = TrimToMax(source.Email, 100);
        target.WebSite = TrimToMax(source.WebSite, 500);
        target.StarLevel = TrimToMax(source.StarLevel, 50);
        target.GpsType = source.GpsType;
        target.Longitude = TrimToMax(source.Longitude, 50);
        target.Latitude = TrimToMax(source.Latitude, 50);
        target.LastModifyTime = source.LastModifyTime;
    }

    static string? TrimToMax(string? value, int maxLength)
    {
        if (string.IsNullOrEmpty(value) || value.Length <= maxLength)
        {
            return value;
        }

        return value[..maxLength];
    }
}
