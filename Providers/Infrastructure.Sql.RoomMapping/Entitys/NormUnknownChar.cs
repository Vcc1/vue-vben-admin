using Infrastructure.Sql.Components;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;

namespace Infrastructure.Sql.RoomMapping.Entitys
{
    public sealed class NormUnknownChar : IHasEntityConfiguration<NormUnknownChar>
    {


        public int CodePoint { get; set; }
        public string Glyph { get; set; } = null!;
        public string Category { get; set; } = null!;
        public DateTime FirstSeen { get; set; }


        public static void Configure(EntityTypeBuilder<NormUnknownChar> b)
        {
            b.ToTable("NormUnknownChar");
            b.HasKey(x => x.CodePoint);
            b.Property(x => x.CodePoint).ValueGeneratedNever();
            b.Property(x => x.Glyph).IsRequired().HasMaxLength(2).UseCollation("Chinese_PRC_BIN2");
            b.Property(x => x.Category).IsRequired().HasMaxLength(50).UseCollation("Chinese_PRC_BIN2");
            b.Property(x => x.FirstSeen).HasColumnType("datetime2");
            b.HasIndex(x => x.Glyph).IsUnique().HasDatabaseName("UX_NormUnknownChar_Glyph");
        }
    }

}