using FSH.Modules.Catalog.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace FSH.Modules.Catalog.Data.Configurations;

public sealed class PriceListConfiguration : IEntityTypeConfiguration<PriceList>
{
    public void Configure(EntityTypeBuilder<PriceList> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);
        builder.ToTable("PriceLists");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Name).IsRequired().HasMaxLength(200);
        builder.HasIndex(x => x.CustomerOrgId);
        builder.HasIndex(x => new { x.ValidFrom, x.ValidTo });

        builder.HasMany(x => x.Lines)
            .WithOne()
            .HasForeignKey(l => l.PriceListId)
            .OnDelete(DeleteBehavior.Cascade);
        builder.Navigation(x => x.Lines)
            .HasField("_lines")
            .UsePropertyAccessMode(PropertyAccessMode.Field)
            .AutoInclude();

        builder.Ignore(x => x.DomainEvents);
    }
}
