using FSH.Modules.Ordering.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace FSH.Modules.Ordering.Data.Configurations;

public sealed class SalesOrderConfiguration : IEntityTypeConfiguration<SalesOrder>
{
    public void Configure(EntityTypeBuilder<SalesOrder> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);
        builder.ToTable("SalesOrders");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.CustomerTenantId).HasMaxLength(64);
        builder.HasIndex(x => new { x.CustomerTenantId, x.StoreId, x.Status });
        builder.Property(x => x.Number).IsRequired().HasMaxLength(32);
        builder.HasIndex(x => x.Number).IsUnique();
        builder.Property(x => x.Status).HasConversion<string>().HasMaxLength(16);
        builder.Property(x => x.WarehouseConfirmationStatus)
            .HasConversion<string>()
            .HasMaxLength(16)
            .HasDefaultValue(WarehouseConfirmationStatus.NotTracked);
        builder.Property(x => x.WarehouseConfirmationDetail).HasMaxLength(1000);
        builder.Property(x => x.PlacementIdempotencyKey).HasMaxLength(200);
        builder.HasIndex(x => new { x.CustomerTenantId, x.PlacementIdempotencyKey })
            .IsUnique()
            .HasFilter("\"PlacementIdempotencyKey\" IS NOT NULL");
        builder.HasIndex(x => x.StoreId);
        builder.HasIndex(x => x.Status);
        builder.HasMany(x => x.Lines)
            .WithOne()
            .HasForeignKey(l => l.SalesOrderId)
            .OnDelete(DeleteBehavior.Cascade);
        builder.Navigation(x => x.Lines)
            .HasField("_lines")
            .UsePropertyAccessMode(PropertyAccessMode.Field)
            .AutoInclude();
        builder.Ignore(x => x.DomainEvents);
    }
}
