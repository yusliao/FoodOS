using Mediator;

namespace FSH.Modules.Catalog.Contracts.v1.CustomerLevels;

/// <summary>Puts a customer into a level; a null level returns the customer to normal catalog pricing.</summary>
public sealed record AssignCustomerLevelCommand(Guid CustomerOrgId, Guid? CustomerLevelId) : ICommand<Unit>;
