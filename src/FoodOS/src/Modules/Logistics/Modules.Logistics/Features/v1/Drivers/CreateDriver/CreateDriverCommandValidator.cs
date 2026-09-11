using FluentValidation;
using FSH.Modules.Logistics.Contracts.v1.Drivers;

namespace FSH.Modules.Logistics.Features.v1.Drivers.CreateDriver;

public sealed class CreateDriverCommandValidator : AbstractValidator<CreateDriverCommand>
{
    public CreateDriverCommandValidator()
    {
        RuleFor(x => x.UserId).NotEmpty();
        RuleFor(x => x.Phone).NotEmpty().MaximumLength(32);
    }
}
