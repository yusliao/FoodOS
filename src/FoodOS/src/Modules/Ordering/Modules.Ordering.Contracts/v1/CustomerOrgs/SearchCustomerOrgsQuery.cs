using FSH.Modules.Ordering.Contracts.Dtos;
using Mediator;

namespace FSH.Modules.Ordering.Contracts.v1.CustomerOrgs;

public sealed record SearchCustomerOrgsQuery(string? Search = null) : IQuery<IReadOnlyList<CustomerOrgDto>>;
