using FluentValidation;
using FSH.Modules.Multitenancy.Contracts.v1.RenewTenant;

namespace FSH.Modules.Multitenancy.Features.v1.RenewTenant;

public sealed class RenewTenantCommandValidator : AbstractValidator<RenewTenantCommand>
{
    public RenewTenantCommandValidator()
    {
        RuleFor(t => t.TenantId).NotEmpty();

        RuleFor(t => t.PlanKey)
            .MaximumLength(64)
            .When(t => !string.IsNullOrWhiteSpace(t.PlanKey))
            .WithMessage("Plan key must not exceed 64 characters.");
    }
}
