using FSH.Modules.Catalog.Contracts.Dtos;
using Mediator;

namespace FSH.Modules.Catalog.Contracts.v1.CustomerLevels;

/// <summary>
/// In-process lookup of the pricing level of each given customer; customers without a level are omitted.
/// Used by other modules (e.g. the order desk) whose users may not hold price-list permissions.
/// </summary>
public sealed record GetCustomerLevelsByCustomersQuery(IReadOnlyList<Guid> CustomerOrgIds)
    : IQuery<IReadOnlyList<CustomerLevelOfCustomerDto>>;
