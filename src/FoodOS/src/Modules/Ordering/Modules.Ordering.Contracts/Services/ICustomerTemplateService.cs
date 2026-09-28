namespace FSH.Modules.Ordering.Contracts.Services;

public interface ICustomerTemplateService
{
    Task EnsureAsync(string tenantId, CancellationToken cancellationToken);
}
