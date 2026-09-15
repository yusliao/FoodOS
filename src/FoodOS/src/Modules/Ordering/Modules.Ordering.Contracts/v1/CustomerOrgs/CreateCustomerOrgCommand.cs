using Mediator;

namespace FSH.Modules.Ordering.Contracts.v1.CustomerOrgs;

public sealed record CreateCustomerOrgCommand(
    string Code,
    string Name,
    bool CreditHold = false,
    string? CustomerTenantId = null) : ICommand<Guid>;
