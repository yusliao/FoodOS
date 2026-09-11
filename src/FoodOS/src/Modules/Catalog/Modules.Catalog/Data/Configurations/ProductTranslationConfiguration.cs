using FSH.Modules.Catalog.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace FSH.Modules.Catalog.Data.Configurations;

public sealed class ProductTranslationConfiguration : IEntityTypeConfiguration<ProductTranslation>
{
    public void Configure(EntityTypeBuilder<ProductTranslation> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);
        builder.ToTable("ProductTranslations");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Culture).IsRequired().HasMaxLength(16);
        builder.Property(x => x.Name).IsRequired().HasMaxLength(200);
        builder.Property(x => x.Description).HasMaxLength(4000);
        builder.HasIndex(x => new { x.ProductId, x.Culture }).IsUnique();
        builder.Ignore(x => x.DomainEvents);
    }
}
