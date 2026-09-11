using FSH.Modules.Logistics.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace FSH.Modules.Logistics.Data.Configurations;

public sealed class ProofOfDeliveryConfiguration : IEntityTypeConfiguration<ProofOfDelivery>
{
    public void Configure(EntityTypeBuilder<ProofOfDelivery> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);
        builder.ToTable("ProofOfDeliveries");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.SignedQtyJson).IsRequired();
        builder.Property(x => x.PhotoFileIds).IsRequired().HasMaxLength(2000);
        builder.Property(x => x.SignerName).IsRequired().HasMaxLength(128);
        builder.Property(x => x.Geo).HasMaxLength(64);
        builder.HasIndex(x => x.StopId).IsUnique();
        builder.Ignore(x => x.DomainEvents);
    }
}
