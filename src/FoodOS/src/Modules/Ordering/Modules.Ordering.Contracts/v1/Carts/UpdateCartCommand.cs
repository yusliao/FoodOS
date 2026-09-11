using FSH.Modules.Ordering.Contracts.Dtos;
using Mediator;

namespace FSH.Modules.Ordering.Contracts.v1.Carts;

public sealed record UpdateCartCommand(
    Guid StoreId,
    IReadOnlyList<CartLineInput> Lines) : ICommand<Guid>;
