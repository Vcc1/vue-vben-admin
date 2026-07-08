using System.Data;
using System.Data.Odbc;
using Dapper;
using Framework.Core.IOC;
using Infrastructures.Sql.MengTuAPI.Entitys;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Infrastructure.Sql.MengTuAPI;

[Service(typeof(IMengTuApiQueryService), ServiceLifetime.Scoped)]
public sealed class MengTuApiQueryService : IMengTuApiQueryService
{
    private const string ConnectionStringName = "MengTuApi";
    private readonly string _connectionString;

    public MengTuApiQueryService(IConfiguration configuration)
    {
        _connectionString = configuration.GetConnectionString(ConnectionStringName)
            ?? throw new InvalidOperationException($"Connection string '{ConnectionStringName}' was not found.");
    }

    public async Task<IReadOnlyList<SupHotelInfo>> GetSupHotelInfosAsync(
        int supId,
        IReadOnlyCollection<string> mtHotelCodes,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(mtHotelCodes);

        var normalizedHotelCodes = mtHotelCodes
            .Where(static x => !string.IsNullOrWhiteSpace(x))
            .Select(static x => x.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();

        if (normalizedHotelCodes.Length == 0)
        {
            return [];
        }

        await using var connection = CreateConnection();
        await connection.OpenAsync(cancellationToken);

        var parameters = new DynamicParameters();
        parameters.Add("SupId", supId, DbType.Int32);

        var placeholders = new string[normalizedHotelCodes.Length];
        for (var i = 0; i < normalizedHotelCodes.Length; i++)
        {
            var parameterName = $"HotelCode{i}";
            placeholders[i] = "?";
            parameters.Add(parameterName, normalizedHotelCodes[i], DbType.String);
        }

        var sql = $"""
            SELECT
                Id,
                SHotelCode,
                SupId,
                SupFlag,
                MTHotelCode,
                State,
                SHotelName_En AS SHotelNameEn,
                SHotelName_CHS AS SHotelNameCHS,
                SContryCode,
                SAddress,
                SCityCode,
                Remark,
                SCityName,
                SContryName,
                SLatitude,
                SLongitude,
                SHotelTel,
                SStarLevel,
                PostCode
            FROM SupHotelInfo
            WHERE SupId = ?
              AND MTHotelCode IN ({string.Join(", ", placeholders)})
              AND SHotelCode IS NOT NULL
              AND LTRIM(RTRIM(SHotelCode)) <> ''
            """;

        var command = new CommandDefinition(
            sql,
            parameters,
            cancellationToken: cancellationToken);

        var result = await connection.QueryAsync<SupHotelInfo>(command);
        return result.AsList();
    }

    private OdbcConnection CreateConnection()
    {
        return new OdbcConnection(_connectionString);
    }
}
