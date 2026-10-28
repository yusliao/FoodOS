using FSH.Modules.Ordering.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace FSH.Modules.Ordering.Data.Configurations;

public sealed class StoreContactConfiguration : IEntityTypeConfiguration<StoreContact>
{
    public void Configure(EntityTypeBuilder<StoreContact> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);
        builder.ToTable("StoreContacts");
        builder.HasKey(x => x.Id);
        // Ids are app-assigned; otherwise EF treats contacts added through Store.Contacts as existing rows.
        builder.Property(x => x.Id).ValueGeneratedNever();
        builder.Property(x => x.Name).IsRequired().HasMaxLength(100);
        builder.Property(x => x.Phone).IsRequired().HasMaxLength(40);
        builder.Property(x => x.PhoneDigits).IsRequired().HasMaxLength(32);
        builder.HasIndex(x => x.PhoneDigits);
        builder.HasIndex(x => new { x.StoreId, x.PhoneDigits }).IsUnique();
        builder.Ignore(x => x.DomainEvents);
    }
}
