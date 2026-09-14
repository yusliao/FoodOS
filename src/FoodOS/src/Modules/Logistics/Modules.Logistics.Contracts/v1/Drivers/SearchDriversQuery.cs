using FSH.Modules.Logistics.Contracts.Dtos;
using Mediator;

namespace FSH.Modules.Logistics.Contracts.v1.Drivers;

public sealed record SearchDriversQuery : IQuery<IReadOnlyList<DriverDto>>;
