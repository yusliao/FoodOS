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

        bool storeExists = await dbContext.Stores
            .AnyAsync(s => s.Id == command.StoreId, cancellationToken)
            .ConfigureAwait(false);
        if (!storeExists)
        {
            throw new NotFoundException($"Store {command.StoreId} not found.");
        }

        var resolved = new List<(Guid ProductId, decimal Quantity, string Zone)>(command.Lines.Count);
        foreach (var line in command.Lines)
        {
            var (_, zone) = await ShopCatalog.GetActiveAsync(mediator, line.ProductId, cancellationToken)
                .ConfigureAwait(false);
            resolved.Add((line.ProductId, line.Quantity, zone.ToString()));
        }

        var cart = await dbContext.Carts
            .FirstOrDefaultAsync(c => c.StoreId == command.StoreId, cancellationToken)
            .ConfigureAwait(false);

        if (cart is null)
        {
            cart = Cart.Create(command.StoreId);
            dbContext.Carts.Add(cart);
        }
        else
        {
            dbContext.CartLines.RemoveRange(cart.Lines);
            cart.Clear();
        }

        cart.ReplaceLines(resolved);
        await dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        return cart.Id;
    }
}
