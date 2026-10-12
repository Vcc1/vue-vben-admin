using System;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Infrastructure.Sql.Components;

namespace Infrastructure.Sql.MengTuYaoChi.Entities;

public sealed class HotelSetting : IHasEntityConfiguration<HotelSetting>
{
    public int Id { get; set; }
    public string MTHotelCode { get; set; } = null!;
    public string OnlinePlatforms { get; set; } = null!;
    public string MatchSuppliers { get; set; } = null!;
    public int? LocalNationalityId { get; set; }
    public string CityName { get; set; } = null!;
    public string CountryName { get; set; } = null!;
    public DateTime CreateTime { get; set; }
    public string CreateBy { get; set; } = null!;
    public DateTime LastUpdateTime { get; set; }
    public string LastUpdateBy { get; set; } = null!;
    public bool IsDeleted { get; set; }
    public bool Status { get; set; }
    public bool? HasCheckPrice { get; set; }
    public string? OrderSuppliers { get; set; }

    public static void Configure(EntityTypeBuilder<HotelSetting> b)
    {
        b.ToTable("HotelSetting");
        b.HasKey(x => x.Id).HasName("PK_HotelSetting");
        b.Property(x => x.Id).ValueGeneratedOnAdd();

        b.Property(x => x.MTHotelCode)
            .IsRequired()
            .HasMaxLength(20)
            .UseCollation("Chinese_PRC_CI_AS");

        b.Property(x => x.OnlinePlatforms)
            .IsRequired()
            .HasMaxLength(200)
            .UseCollation("Chinese_PRC_CI_AS");

        b.Property(x => x.MatchSuppliers)
            .IsRequired()
            .HasMaxLength(500)
            .UseCollation("Chinese_PRC_CI_AS");

        b.Property(x => x.LocalNationalityId);

        b.Property(x => x.CityName)
            .IsRequired()
            .HasMaxLength(200)
            .UseCollation("Chinese_PRC_CI_AS");

        b.Property(x => x.CountryName)
            .IsRequired()
            .HasMaxLength(200)
            .UseCollation("Chinese_PRC_CI_AS");

        b.Property(x => x.CreateTime).HasColumnType("datetime");
        b.Property(x => x.CreateBy).IsRequired().HasMaxLength(50).UseCollation("Chinese_PRC_CI_AS");
        b.Property(x => x.LastUpdateTime).HasColumnType("datetime");
        b.Property(x => x.LastUpdateBy).IsRequired().HasMaxLength(50).UseCollation("Chinese_PRC_CI_AS");
        b.Property(x => x.IsDeleted).IsRequired();
        b.Property(x => x.Status).IsRequired();
        b.Property(x => x.HasCheckPrice);
        b.Property(x => x.OrderSuppliers).HasMaxLength(200).UseCollation("Chinese_PRC_CI_AS");

        b.HasIndex(x => x.MTHotelCode)
            .IsUnique()
            .HasDatabaseName("NonClusteredIndex-20240322-170903");
    }
}
