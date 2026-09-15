namespace FSH.Modules.Ordering.Contracts.Dtos;

public sealed record CustomerOrgDto(
    Guid Id,
    string? CustomerTenantId,
    string Code,
    string Name,
    bool CreditHold,
    DateTime CreatedAtUtc);
