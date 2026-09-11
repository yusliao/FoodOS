using FSH.Modules.Procurement.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace FSH.Modules.Procurement.Data.Configurations;

public sealed class ReceiveRecordConfiguration : IEntityTypeConfiguration<ReceiveRecord>
{
    public void Configure(EntityTypeBuilder<ReceiveRecord> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);
        builder.ToTable("ReceiveRecords");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Quantity).HasPrecision(18, 3);
        builder.Property(x => x.Zone).IsRequired().HasMaxLength(16);
        builder.HasIndex(x => x.QualityCheckId);
    }
}
