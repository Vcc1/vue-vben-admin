using Infrastructure.Sql.RoomMapping;
using Infrastructure.Sql.RoomMapping.Entitys;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace HotelMatchingPipeline.Infra.Tests;

public sealed class OtaEntityMappingTests
{
    [Fact]
    public void Ota_entities_use_hotel_id_keys_and_current_phone_columns()
    {
        var options = new DbContextOptionsBuilder<RoomMappingDbContext>()
            .UseSqlServer("Server=(local);Database=ModelOnly;Trusted_Connection=True;TrustServerCertificate=True")
            .Options;
        using var database = new RoomMappingDbContext(options);

        var raw = database.Model.FindEntityType(typeof(OTAHotelInfoRaw))!;
        Assert.Equal([nameof(OTAHotelInfoRaw.HotelID)], raw.FindPrimaryKey()!.Properties.Select(x => x.Name));
        Assert.Null(raw.FindProperty("Id"));

        var clean = database.Model.FindEntityType(typeof(OTAHotelInfo))!;
        Assert.Equal([nameof(OTAHotelInfo.HotelID)], clean.FindPrimaryKey()!.Properties.Select(x => x.Name));
        Assert.Null(clean.FindProperty("Id"));
        Assert.Null(clean.FindProperty("RawId"));
        Assert.False(clean.IsSqlOutputClauseUsed());
        var cleanIndex = clean.GetIndexes().Single(index => index.GetDatabaseName() == "IX_OTAHotelInfo_Clean");
        Assert.Equal(
            [
                nameof(OTAHotelInfo.Estate),
                nameof(OTAHotelInfo.CleanStatus),
                nameof(OTAHotelInfo.DataQualityStatus),
                nameof(OTAHotelInfo.CleanVersion)
            ],
            cleanIndex.Properties.Select(x => x.Name));
        Assert.Contains(clean.GetIndexes(), index => index.GetDatabaseName() == "IX_OTAHotelInfo_CleaningSnapshot");

        var phone = database.Model.FindEntityType(typeof(OTAHotelPhone))!;
        Assert.Equal(
            [nameof(OTAHotelPhone.HotelID), nameof(OTAHotelPhone.Ordinal)],
            phone.FindPrimaryKey()!.Properties.Select(x => x.Name));
        Assert.NotNull(phone.FindProperty(nameof(OTAHotelPhone.SourcePhone)));
        Assert.NotNull(phone.FindProperty(nameof(OTAHotelPhone.NormalizedPhone)));
        Assert.NotNull(phone.FindProperty(nameof(OTAHotelPhone.CountryIsoCode)));
        Assert.Null(phone.FindProperty("OTAHotelInfoId"));
        Assert.Null(phone.FindProperty("E164"));
    }

    [Fact]
    public void Supplier_entities_share_the_supplier_hotel_id_key()
    {
        var options = new DbContextOptionsBuilder<RoomMappingDbContext>()
            .UseSqlServer("Server=(local);Database=ModelOnly;Trusted_Connection=True;TrustServerCertificate=True")
            .Options;
        using var database = new RoomMappingDbContext(options);

        var raw = database.Model.FindEntityType(typeof(SupHotelInfoRaw))!;
        Assert.Equal([nameof(SupHotelInfoRaw.SupHotelId)], raw.FindPrimaryKey()!.Properties.Select(x => x.Name));
        Assert.Null(raw.FindProperty("Id"));
        Assert.Null(raw.FindProperty("SourceId"));

        var clean = database.Model.FindEntityType(typeof(SupHotelInfoClean))!;
        Assert.Equal([nameof(SupHotelInfoClean.SupHotelId)], clean.FindPrimaryKey()!.Properties.Select(x => x.Name));
        Assert.NotNull(clean.FindProperty(nameof(SupHotelInfoClean.MTHotelCode)));
        Assert.NotNull(clean.FindProperty(nameof(SupHotelInfoClean.CountryCode)));
        Assert.NotNull(clean.FindProperty(nameof(SupHotelInfoClean.CountryName)));
        Assert.NotNull(clean.FindProperty(nameof(SupHotelInfoClean.CityCode)));
        Assert.NotNull(clean.FindProperty(nameof(SupHotelInfoClean.CityName)));
        Assert.Null(clean.FindProperty("Id"));
        Assert.Null(clean.FindProperty("RawId"));
        Assert.Null(clean.FindProperty("ExistingMTHotelCode"));
        Assert.Contains(clean.GetIndexes(), index => index.GetDatabaseName() == "IX_SupHotelInfo_CleaningSnapshot");

        var phone = database.Model.FindEntityType(typeof(SupHotelPhone))!;
        Assert.NotNull(phone.FindProperty(nameof(SupHotelPhone.SupHotelId)));
        Assert.Null(phone.FindProperty("SupHotelInfoId"));

        var failure = database.Model.FindEntityType(typeof(HotelCleaningFailure))!;
        Assert.Contains(
            failure.GetIndexes(),
            index => index.GetDatabaseName() == "IX_HotelCleaningFailure_CleaningSnapshot");

        var matchResult = database.Model.FindEntityType(typeof(HotelMatchResult))!;
        var businessKey = matchResult.GetIndexes().Single(
            index => index.GetDatabaseName() == "UX_HotelMatchResult_BusinessKey");
        Assert.True(businessKey.IsUnique);
        Assert.Equal(
            [nameof(HotelMatchResult.SupHotelId)],
            businessKey.Properties.Select(property => property.Name));
    }
}
