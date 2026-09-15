using FSH.Modules.Ordering.Contracts.Access;
using FSH.Modules.Ordering.Contracts.v1.StoreAccess;
using Mediator;

namespace FSH.Modules.Ordering.Features.v1.StoreAccess.GetMyStoreAccess;

public sealed class GetMyStoreAccessQueryHandler(ICustomerAccessScopeResolver resolver)
    : IQueryHandler<GetMyStoreAccessQuery, CustomerAccessScope>
{
    public async ValueTask<CustomerAccessScope> Handle(
        GetMyStoreAccessQuery query,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);
        return await resolver.ResolveCurrentAsync(cancellationToken).ConfigureAwait(false);
    }
}
