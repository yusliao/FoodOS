using FSH.Framework.Core.Exceptions;
using FSH.Modules.Catalog.Contracts.v1.Products;
using FSH.Modules.Catalog.Data;
using FSH.Modules.Catalog.Domain;
using Mediator;
using Microsoft.EntityFrameworkCore;

namespace FSH.Modules.Catalog.Features.v1.Products.UpsertProductTranslation;

public sealed class UpsertProductTranslationCommandHandler(CatalogDbContext dbContext)
    : ICommandHandler<UpsertProductTranslationCommand, Guid>
{
    public async ValueTask<Guid> Handle(UpsertProductTranslationCommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        var product = await dbContext.Products
            .FirstOrDefaultAsync(p => p.Id == command.ProductId, cancellationToken)
            .ConfigureAwait(false)
            ?? throw new NotFoundException($"Product {command.ProductId} not found.");

        string culture = ProductTranslation.NormalizeCulture(command.Culture);
        var existing = product.Translations.FirstOrDefault(t =>
            string.Equals(t.Culture, culture, StringComparison.OrdinalIgnoreCase));

        if (existing is not null)
        {
            existing.Update(command.Name, command.Description);
        }
        else
        {
            dbContext.ProductTranslations.Add(
                ProductTranslation.Create(product.Id, culture, command.Name, command.Description));
        }

        await dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        return product.Id;
    }
}
