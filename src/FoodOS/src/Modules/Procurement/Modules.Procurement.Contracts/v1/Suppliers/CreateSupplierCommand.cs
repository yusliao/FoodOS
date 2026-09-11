using Mediator;

namespace FSH.Modules.Procurement.Contracts.v1.Suppliers;

public sealed record CreateSupplierCommand(
    string Code,
    string Name,
    string? Categories = null,
    int LeadDays = 0) : ICommand<Guid>;
