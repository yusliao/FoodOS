using Mediator;

namespace FSH.Modules.Logistics.Contracts.v1.Vehicles;

public sealed record CreateVehicleCommand(
    string Plate,
    string CompartmentZones,
    decimal PayloadKg) : ICommand<Guid>;
