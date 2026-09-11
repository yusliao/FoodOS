using FSH.Modules.Logistics.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace FSH.Modules.Logistics.Data.Configurations;

public sealed class ShipmentStopConfiguration : IEntityTypeConfiguration<ShipmentStop>
{
    public void Configure(EntityTypeBuilder<ShipmentStop> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);
        builder.ToTable("ShipmentStops");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Window).HasMaxLength(32);
        builder.Property(x => x.Status).HasConversion<string>().HasMaxLength(16);
        builder.HasIndex(x => new { x.ShipmentId, x.Sequence }).IsUnique();
        builder.HasOne(x => x.ProofOfDelivery)
            .WithOne()
            .HasForeignKey<ProofOfDelivery>(p => p.StopId)
            .OnDelete(DeleteBehavior.Cascade);
        builder.Navigation(x => x.ProofOfDelivery).AutoInclude();
        builder.Ignore(x => x.DomainEvents);
    }
}
