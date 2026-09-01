using System;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Infrastructure.Sql.Components;

namespace Infrastructure.Sql.MengTuYaoChi.Entities;

public sealed class CachePlanSetting : IHasEntityConfiguration<CachePlanSetting>
{
    public int Id { get; set; }
    public int BeginPriceDay { get; set; }
    public int EndPriceDay { get; set; }
    public int StayDays { get; set; }
    public int Adults { get; set; }
    public int Children { get; set; }
    public decimal FreshHour { get; set; }
    public decimal QpsRatio { get; set; }
    public int Weight { get; set; }
    public string? AssignSuppliers { get; set; }
    public int Status { get; set; }
    public DateTime CreateTime { get; set; }
    public string CreateBy { get; set; } = null!;
    public DateTime LastUpdateTime { get; set; }
    public string LastUpdateBy { get; set; } = null!;
    public bool IsDeleted { get; set; }

    public static void Configure(EntityTypeBuilder<CachePlanSetting> b)
    {
        b.ToTable("CachePlanSetting");
        b.HasKey(x => x.Id).HasName("PK_PlatformPlanSetting");
        b.Property(x => x.Id).ValueGeneratedOnAdd();

        b.Property(x => x.BeginPriceDay).IsRequired();
        b.Property(x => x.EndPriceDay).IsRequired();
        b.Property(x => x.StayDays).IsRequired();
        b.Property(x => x.Adults).IsRequired();
        b.Property(x => x.Children).IsRequired();
        b.Property(x => x.FreshHour).HasColumnType("decimal(18,2)");
        b.Property(x => x.QpsRatio).HasColumnType("decimal(18,2)");
        b.Property(x => x.Weight).IsRequired();
        b.Property(x => x.AssignSuppliers).HasMaxLength(100).UseCollation("Chinese_PRC_CI_AS");
        b.Property(x => x.Status).IsRequired();
        b.Property(x => x.CreateTime).HasColumnType("datetime");
        b.Property(x => x.CreateBy).IsRequired().HasMaxLength(50).UseCollation("Chinese_PRC_CI_AS");
        b.Property(x => x.LastUpdateTime).HasColumnType("datetime");
        b.Property(x => x.LastUpdateBy).IsRequired().HasMaxLength(50).UseCollation("Chinese_PRC_CI_AS");
        b.Property(x => x.IsDeleted).IsRequired();
    }
}
