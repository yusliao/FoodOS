using FSH.Modules.Catalog.Contracts.Dtos;
using Mediator;

namespace FSH.Modules.Catalog.Contracts.v1.CustomerLevels;

public sealed record GetCustomerLevelAssignmentsQuery : IQuery<IReadOnlyList<CustomerLevelAssignmentDto>>;
