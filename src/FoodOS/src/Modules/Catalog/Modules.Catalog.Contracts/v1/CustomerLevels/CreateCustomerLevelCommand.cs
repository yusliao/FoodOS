using Mediator;

namespace FSH.Modules.Catalog.Contracts.v1.CustomerLevels;

public sealed record CreateCustomerLevelCommand(string Code, string Name, decimal DiscountPercent) : ICommand<Guid>;
