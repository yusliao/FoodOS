using Mediator;

namespace FSH.Modules.Ordering.Contracts.v1.Orders;

public sealed record ConfirmOrderPackedCommand(Guid OrderId) : ICommand<Guid>;
