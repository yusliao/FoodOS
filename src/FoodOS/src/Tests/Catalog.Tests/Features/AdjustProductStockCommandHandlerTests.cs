using System.Net;
using FSH.Framework.Core.Exceptions;
using FSH.Modules.Catalog.Contracts.v1.Products;
using FSH.Modules.Catalog.Features.v1.Products.AdjustProductStock;

namespace Catalog.Tests.Features;

public sealed class AdjustProductStockCommandHandlerTests
{
    [Fact]
    public void Handle_Should_ThrowGone()
    {
        var handler = new AdjustProductStockCommandHandler();
        var command = new AdjustProductStockCommand(Guid.NewGuid(), 5);

        var ex = Should.Throw<CustomException>(() =>
            _ = handler.Handle(command, CancellationToken.None).AsTask().GetAwaiter().GetResult());

        ex.StatusCode.ShouldBe(HttpStatusCode.Gone);
        ex.Message.ShouldContain("Inventory");
    }
}
