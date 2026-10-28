namespace FSH.Modules.Identity.Contracts.Authorization;

/// <summary>An operator position the root tenant should start with.</summary>
public sealed record OperatorRoleTemplate(string Name, string Description, IReadOnlyList<string> Permissions);

/// <summary>
/// Implemented by modules that contribute operator positions. The identity seed creates each
/// missing role in the root tenant once; roles that already exist are left as administrators edited them.
/// </summary>
public interface IOperatorRoleTemplateSource
{
    IEnumerable<OperatorRoleTemplate> GetTemplates();
}
