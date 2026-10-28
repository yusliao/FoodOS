using Mediator;

namespace FSH.Modules.Catalog.Contracts.v1.CustomerLevels;

public sealed record UpdateCustomerLevelCommand(Guid Id, string Code, string Name, decimal DiscountPercent) : ICommand<Unit>;
