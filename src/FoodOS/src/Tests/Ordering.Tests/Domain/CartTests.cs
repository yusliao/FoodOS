using FSH.Modules.Ordering.Domain;

namespace Ordering.Tests.Domain;

public sealed class CartTests
{
    [Fact]
    public void ReplaceLines_Should_ReplaceCollection()
    {
        var cart = Cart.Create(Guid.CreateVersion7());
        var productId = Guid.CreateVersion7();

        cart.ReplaceLines([(productId, 3m, "Chilled")]);

        cart.Lines.Count.ShouldBe(1);
        cart.Lines[0].ProductId.ShouldBe(productId);
        cart.Lines[0].Quantity.ShouldBe(3m);
        cart.Lines[0].Zone.ShouldBe("Chilled");

        cart.Clear();
        cart.Lines.ShouldBeEmpty();
    }
}
