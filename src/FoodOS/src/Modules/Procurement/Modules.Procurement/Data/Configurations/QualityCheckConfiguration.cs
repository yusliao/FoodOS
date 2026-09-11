using FSH.Modules.Procurement.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace FSH.Modules.Procurement.Data.Configurations;

public sealed class QualityCheckConfiguration : IEntityTypeConfiguration<QualityCheck>
{
    public void Configure(EntityTypeBuilder<QualityCheck> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);
        builder.ToTable("QualityChecks");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Result).HasConversion<string>().HasMaxLength(8);
        builder.Property(x => x.SampleQty).HasPrecision(18, 3);
        builder.Property(x => x.Quantity).HasPrecision(18, 3);
        builder.Property(x => x.LotNo).IsRequired().HasMaxLength(64);
        builder.Property(x => x.Note).HasMaxLength(512);
        builder.Property(x => x.PhotoFileIds).HasMaxLength(1024);
        builder.HasIndex(x => x.PurchaseOrderId);
        builder.HasIndex(x => x.LineId);
    }
}
