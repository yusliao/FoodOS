using Microsoft.AspNetCore.Identity;
using FSH.Framework.Shared.Constants;

namespace FSH.Modules.Identity.Domain;

public class FshRole : IdentityRole
{
    public string? Description { get; set; }
    public string Audience { get; set; }

    public FshRole(string name, string? description = null, string audience = RoleAudiences.Customer)
        : base(name)
    {
        ArgumentNullException.ThrowIfNull(name);

        Description = description;
        Audience = audience;
        NormalizedName = name.ToUpperInvariant();
    }
}
