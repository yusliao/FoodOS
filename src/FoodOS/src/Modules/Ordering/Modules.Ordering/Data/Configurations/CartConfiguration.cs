using FSH.Modules.Ordering.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace FSH.Modules.Ordering.Data.Configurations;

public sealed class CartConfiguration : IEntityTypeConfiguration<Cart>
{
    public void Configure(EntityTypeBuilder<Cart> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);
        builder.ToTable("Carts");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.CustomerTenantId).HasMaxLength(64);
        builder.HasIndex(x => new { x.CustomerTenantId, x.StoreId });
        builder.HasIndex(x => x.StoreId).IsUnique();
        builder.HasMany(x => x.Lines)
            .WithOne()
            .HasForeignKey(l => l.CartId)
            .OnDelete(DeleteBehavior.Cascade);
        builder.Navigation(x => x.Lines)
            .HasField("_lines")
            .UsePropertyAccessMode(PropertyAccessMode.Field)
            .AutoInclude();
        builder.Ignore(x => x.DomainEvents);
    }
}
