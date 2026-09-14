using FSH.Modules.Logistics.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace FSH.Modules.Logistics.Data.Configurations;

public sealed class DispatchReminderLogConfiguration : IEntityTypeConfiguration<DispatchReminderLog>
{
    public void Configure(EntityTypeBuilder<DispatchReminderLog> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);
        builder.ToTable("DispatchReminderLogs");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Kind).HasMaxLength(16).IsRequired();
        builder.HasIndex(x => new { x.WarehouseId, x.LocalDate, x.Kind }).IsUnique();
        builder.Ignore(x => x.DomainEvents);
    }
}
