namespace FSH.Modules.Identity.Contracts.DTOs;

public class RoleDto
{
    public string Id { get; set; } = default!;
    public string Name { get; set; } = default!;
    public string? Description { get; set; }
    public string Audience { get; set; } = default!;
    public IReadOnlyCollection<string>? Permissions { get; set; }
}
