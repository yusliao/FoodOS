using Mediator;

namespace FSH.Modules.Ordering.Contracts.v1.CustomerOrgs;

/// <summary>Renames a customer and optionally changes its code (unique across customers; null keeps it).</summary>
public sealed record UpdateCustomerOrgCommand(Guid CustomerOrgId, string Name, string? Code = null) : ICommand;
