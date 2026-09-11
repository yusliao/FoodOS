using FSH.Modules.Ordering.Contracts.Dtos;
using Mediator;

namespace FSH.Modules.Ordering.Contracts.v1.Orders;

public sealed record AmendOrderCommand(
    Guid OrderId,
    IReadOnlyList<AmendOrderLineInput> Lines) : ICommand<Guid>;
