using FSH.Modules.Ordering.Contracts.Access;
using Mediator;

namespace FSH.Modules.Ordering.Contracts.v1.StoreAccess;

public sealed record GetMyStoreAccessQuery : IQuery<CustomerAccessScope>;
