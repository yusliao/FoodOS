using System.Net;
using FSH.Framework.Core.Exceptions;
using FSH.Modules.Catalog.Contracts.v1.Products;
using FSH.Modules.Catalog.Data;
using FSH.Modules.Catalog.Domain;
using Mediator;
using Microsoft.EntityFrameworkCore;

namespace FSH.Modules.Catalog.Features.v1.Products.UpdateProductFulfillment;

public sealed class UpdateProductFulfillmentCommandHandler(CatalogDbContext dbContext)
    : ICommandHandler<UpdateProductFulfillmentCommand, Guid>
{
    public async ValueTask<Guid> Handle(UpdateProductFulfillmentCommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        var product = await dbContext.Products
            .FirstOrDefaultAsync(p => p.Id == command.ProductId, cancellationToken)
            .ConfigureAwait(false)
            ?? throw new NotFoundException($"Product {command.ProductId} not found.");

        if (!Enum.TryParse(command.TemperatureZone, ignoreCase: true, out TemperatureZone zone))
        {
            throw new CustomException(
                $"Unknown temperature zone '{command.TemperatureZone}'.",
                (IEnumerable<string>?)null,
                HttpStatusCode.BadRequest);
        }

        product.SetFulfillmentAttributes(
            zone,
            command.ShelfLifeDays,
            command.MinRemainingDaysOnShip,
            command.BaseUom,
            command.CatchWeight,
            command.Barcode,
            command.StorageNote);

        await dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        return product.Id;
    }
}
