using System.Net;
using FSH.Framework.Core.Exceptions;
using FSH.Modules.Catalog.Contracts.v1.Products;
using Mediator;

namespace FSH.Modules.Catalog.Features.v1.Products.AdjustProductStock;

public sealed class AdjustProductStockCommandHandler
    : ICommandHandler<AdjustProductStockCommand, int>
{
    public ValueTask<int> Handle(AdjustProductStockCommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);
        _ = cancellationToken;
        throw new CustomException(
            AdjustProductStockEndpoint.GoneDetail,
            (IEnumerable<string>?)null,
            HttpStatusCode.Gone);
    }
}
