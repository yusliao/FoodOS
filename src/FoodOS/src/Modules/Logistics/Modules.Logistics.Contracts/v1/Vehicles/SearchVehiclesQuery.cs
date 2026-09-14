using FSH.Modules.Logistics.Contracts.Dtos;
using Mediator;

namespace FSH.Modules.Logistics.Contracts.v1.Vehicles;

public sealed record SearchVehiclesQuery : IQuery<IReadOnlyList<VehicleDto>>;
