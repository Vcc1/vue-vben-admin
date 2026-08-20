using Infrastructure.Sql.RoomMapping;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;

namespace Tests.Host.HotelMappingSync;

public interface IHotelMappingSourceReader
{
    Task<IReadOnlyList<HotelMappingSourceRow>> ReadAsync(CancellationToken cancellationToken);
}
public sealed class HotelMappingSourceReader(
    RoomMappingDbContext dbContext,
    ILogger<HotelMappingSourceReader> logger) : IHotelMappingSourceReader
{
    const int MaxAttempts = 3;

    public async Task<IReadOnlyList<HotelMappingSourceRow>> ReadAsync(
        CancellationToken cancellationToken)
    {
        var connection = dbContext.Database.GetDbConnection();
        var connectionString = new SqlConnectionStringBuilder(connection.ConnectionString)
        {
            ConnectTimeout = 30
        };
        connection.ConnectionString = connectionString.ConnectionString;
        dbContext.Database.SetCommandTimeout(TimeSpan.FromMinutes(3));

        for (var attempt = 1; ; attempt++)
        {
            try
            {
                return await dbContext.HotelMatchResults
                    .AsNoTracking()
                    .Where(row => row.MTHotelCode != null && row.MTHotelCode != string.Empty)
                    .Select(row => new HotelMappingSourceRow(
                        row.SupplierHotel.SupId,
                        row.SupplierHotel.SHotelCode,
                        row.MTHotelCode,
                        row.SupplierHotel.CountryCode,
                        row.SupplierHotel.CityCode,
                        row.SupplierHotel.HotelName,
                        row.SupplierHotel.HotelNameEn))
                    .ToArrayAsync(cancellationToken);
            }
            catch (SqlException ex) when (ex.Number == -2 && attempt < MaxAttempts)
            {
                logger.LogWarning(
                    "RoomMapping SQL connection timed out. Retrying attempt {NextAttempt} of {MaxAttempts}.",
                    attempt + 1,
                    MaxAttempts);
                await dbContext.Database.CloseConnectionAsync();
                await Task.Delay(TimeSpan.FromSeconds(attempt * 2), cancellationToken);
            }
        }
    }
}
