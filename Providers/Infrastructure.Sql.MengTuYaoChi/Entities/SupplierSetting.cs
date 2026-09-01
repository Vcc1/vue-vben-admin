using System;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Infrastructure.Sql.Components;

namespace Infrastructure.Sql.MengTuYaoChi.Entities;

public sealed class SupplierSetting : IHasEntityConfiguration<SupplierSetting>
{
    public int Id { get; set; }
    public int SupId { get; set; }
    public string SupFlag { get; set; } = null!;
    public string? SupCode { get; set; }
    public bool EnabledCache { get; set; }
    public bool EnableBatchSearch { get; set; }
    public bool? CountryWideAstrict { get; set; }
    public bool? CityWideAstrict { get; set; }
    public int MaxBatchSearchHotelNums { get; set; }
    public int TimeOut { get; set; }
    public int MaxQps { get; set; }
    public int MaxSearchQueue { get; set; }
    public bool HasMultiNationality { get; set; }
    public bool? OnlyRunHasCheckPriceHotel { get; set; }
    public string? RunHotelTypes { get; set; }
    public string? OnlinePlatforms { get; set; }
    public DateTime CreateTime { get; set; }
    public string CreateBy { get; set; } = null!;
    public DateTime LastUpdateTime { get; set; }
    public string LastUpdateBy { get; set; } = null!;
    public bool IsDeleted { get; set; }

    public static void Configure(EntityTypeBuilder<SupplierSetting> b)
    {
        b.ToTable("SupplierSetting");
        b.HasKey(x => x.Id).HasName("PK_SupplierSetting");
        b.Property(x => x.Id).ValueGeneratedOnAdd();

        b.Property(x => x.SupId).IsRequired();
        b.Property(x => x.SupFlag).IsRequired().HasMaxLength(20).UseCollation("Chinese_PRC_CI_AS");
        b.Property(x => x.SupCode).HasMaxLength(50).UseCollation("Chinese_PRC_CI_AS");
        b.Property(x => x.EnabledCache).IsRequired();
        b.Property(x => x.EnableBatchSearch).IsRequired();
        b.Property(x => x.CountryWideAstrict);
        b.Property(x => x.CityWideAstrict);
        b.Property(x => x.MaxBatchSearchHotelNums).IsRequired();
        b.Property(x => x.TimeOut).IsRequired();
        b.Property(x => x.MaxQps).IsRequired();
        b.Property(x => x.MaxSearchQueue).IsRequired();
        b.Property(x => x.HasMultiNationality).IsRequired();
        b.Property(x => x.OnlyRunHasCheckPriceHotel);
        b.Property(x => x.RunHotelTypes).HasMaxLength(50).UseCollation("Chinese_PRC_CI_AS");
        b.Property(x => x.OnlinePlatforms).HasMaxLength(50).UseCollation("Chinese_PRC_CI_AS");
        b.Property(x => x.CreateTime).HasColumnType("datetime");
        b.Property(x => x.CreateBy).IsRequired().HasMaxLength(50).UseCollation("Chinese_PRC_CI_AS");
        b.Property(x => x.LastUpdateTime).HasColumnType("datetime");
        b.Property(x => x.LastUpdateBy).IsRequired().HasMaxLength(50).UseCollation("Chinese_PRC_CI_AS");
        b.Property(x => x.IsDeleted).IsRequired();

        b.HasIndex(x => x.SupId)
            .IsUnique()
            .HasDatabaseName("NonClusteredIndex-20241106-111558");
    }
}
