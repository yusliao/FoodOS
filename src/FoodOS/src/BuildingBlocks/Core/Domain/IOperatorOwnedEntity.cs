namespace FSH.Framework.Core.Domain;

/// <summary>
/// Marks data owned by the single FoodOS operator rather than by the requesting
/// restaurant tenant. During the migration window the persistence layer retains
/// the legacy TenantId column and reads the explicit root operator source only.
/// </summary>
public interface IOperatorOwnedEntity : IGlobalEntity { }
