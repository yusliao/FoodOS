using FSH.Framework.Core.Exceptions;
using FSH.Modules.Ordering.Contracts.v1.Carts;
using FSH.Modules.Ordering.Data;
using FSH.Modules.Ordering.Domain;
using Mediator;
using Microsoft.EntityFrameworkCore;

namespace FSH.Modules.Ordering.Features.v1.Carts.UpdateCart;

public sealed class UpdateCartCommandHandler(OrderingDbContext dbContext, IMediator mediator)
    : ICommandHandler<UpdateCartCommand, Guid>
{
    public async ValueTask<Guid> Handle(UpdateCartCommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        var store = await dbContext.Stores
            .AsNoTracking()
            .FirstOrDefaultAsync(s => s.Id == command.StoreId, cancellationToken)
            .ConfigureAwait(false);
        if (store is null)
        {
            throw new NotFoundException($"Store {command.StoreId} not found.");
        }

        var products = await ShopCatalog.GetActiveManyAsync(
                mediator,
                command.Lines.Select(line => line.ProductId),
                cancellationToken)
            .ConfigureAwait(false);
        var resolved = new List<(Guid ProductId, decimal Quantity, string Zone)>(command.Lines.Count);
        foreach (var line in command.Lines)
        {
            var (_, zone) = products[line.ProductId];
            resolved.Add((line.ProductId, line.Quantity, zone.ToString()));
        }

        var cart = await dbContext.Carts
            .FirstOrDefaultAsync(c => c.StoreId == command.StoreId, cancellationToken)
            .ConfigureAwait(false);

        if (cart is null)
        {
            cart = Cart.Create(command.StoreId, store.CustomerTenantId);
            dbContext.Carts.Add(cart);
        }
        else
        {
            var existingLines = cart.Lines.ToList();
            if (existingLines.Count > 0)
            {
                dbContext.CartLines.RemoveRange(existingLines);
            }
        }

        cart.ReplaceLines(resolved);
        if (dbContext.Entry(cart).State != EntityState.Added)
        {
            dbContext.CartLines.AddRange(cart.Lines);
        }

        await dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        return cart.Id;
    }
}
