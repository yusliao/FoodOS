using FSH.Modules.Procurement.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace FSH.Modules.Procurement.Data.Configurations;

public sealed class PurchaseOrderConfiguration : IEntityTypeConfiguration<PurchaseOrder>
{
    public void Configure(EntityTypeBuilder<PurchaseOrder> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);
        builder.ToTable("PurchaseOrders");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Number).IsRequired().HasMaxLength(32);
        builder.HasIndex(x => x.Number).IsUnique();
        builder.Property(x => x.Status).HasConversion<string>().HasMaxLength(16);
        builder.HasIndex(x => x.SupplierId);
        builder.HasIndex(x => x.Status);
        builder.HasMany(x => x.Lines)
            .WithOne()
            .HasForeignKey(l => l.PurchaseOrderId)
            .OnDelete(DeleteBehavior.Cascade);
        builder.Navigation(x => x.Lines)
            .HasField("_lines")
            .UsePropertyAccessMode(PropertyAccessMode.Field)
            .AutoInclude();
        builder.HasMany(x => x.QualityChecks)
            .WithOne()
            .HasForeignKey(c => c.PurchaseOrderId)
            .OnDelete(DeleteBehavior.Cascade);
        builder.Navigation(x => x.QualityChecks)
            .HasField("_qualityChecks")
            .UsePropertyAccessMode(PropertyAccessMode.Field)
            .AutoInclude();
        builder.HasMany(x => x.ReceiveRecords)
            .WithOne()
            .HasForeignKey(r => r.PurchaseOrderId)
            .OnDelete(DeleteBehavior.Cascade);
        builder.Navigation(x => x.ReceiveRecords)
            .HasField("_receiveRecords")
            .UsePropertyAccessMode(PropertyAccessMode.Field)
            .AutoInclude();
        builder.HasOne(x => x.Appointment)
            .WithOne()
            .HasForeignKey<InboundAppointment>(a => a.PurchaseOrderId)
            .OnDelete(DeleteBehavior.Cascade);
        builder.Navigation(x => x.Appointment).AutoInclude();
        builder.Ignore(x => x.DomainEvents);
    }
}
