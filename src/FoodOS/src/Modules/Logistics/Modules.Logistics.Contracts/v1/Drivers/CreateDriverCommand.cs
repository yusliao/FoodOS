using Mediator;

namespace FSH.Modules.Logistics.Contracts.v1.Drivers;

public sealed record CreateDriverCommand(Guid UserId, string Phone) : ICommand<Guid>;
