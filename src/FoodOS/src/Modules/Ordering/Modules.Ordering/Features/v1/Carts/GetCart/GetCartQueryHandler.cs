using FSH.Framework.Core.Exceptions;
using FSH.Modules.Ordering.Contracts.Dtos;
using FSH.Modules.Ordering.Contracts.v1.Carts;
using FSH.Modules.Ordering.Data;
using Mediator;
using Microsoft.EntityFrameworkCore;

namespace FSH.Modules.Ordering.Features.v1.Carts.GetCart;

public sealed class GetCartQueryHandler(OrderingDbContext dbContext)
    : IQueryHandler<GetCartQuery, CartDto>
{
    public async ValueTask<CartDto> Handle(GetCartQuery query, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);

        bool storeExists = await dbContext.Stores
            .AsNoTracking()
            .AnyAsync(s => s.Id == query.StoreId, cancellationToken)
            .ConfigureAwait(false);
        if (!storeExists)
        {
            throw new NotFoundException($"Store {query.StoreId} not found.");
        }

        var cart = await dbContext.Carts
            .AsNoTracking()
            .FirstOrDefaultAsync(c => c.StoreId == query.StoreId, cancellationToken)
            .ConfigureAwait(false);

        return cart is null ? OrderingMappings.EmptyCart(query.StoreId) : cart.ToDto();
    }
}
