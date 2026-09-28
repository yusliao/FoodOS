using Mediator;

namespace FSH.Modules.Ordering.Contracts.v1.CustomerOrgs;

public sealed record UpdateCustomerOrgCommand(Guid CustomerOrgId, string Name) : ICommand;
