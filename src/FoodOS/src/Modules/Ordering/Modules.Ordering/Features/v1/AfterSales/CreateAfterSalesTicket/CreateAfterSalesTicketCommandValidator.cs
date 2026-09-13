using FluentValidation;
using FSH.Modules.Ordering.Contracts.v1.AfterSales;
using FSH.Modules.Ordering.Domain;

namespace FSH.Modules.Ordering.Features.v1.AfterSales.CreateAfterSalesTicket;

public sealed class CreateAfterSalesTicketCommandValidator : AbstractValidator<CreateAfterSalesTicketCommand>
{
    public CreateAfterSalesTicketCommandValidator()
    {
        RuleFor(x => x.OrderId).NotEmpty();
        RuleFor(x => x.OrderLineId).NotEmpty();
        RuleFor(x => x.Type)
            .NotEmpty()
            .Must(t => Enum.TryParse<AfterSalesTicketType>(t, ignoreCase: true, out _))
            .WithMessage("Type must be Shortage, Damage, or Return.");
        RuleFor(x => x.Quantity).GreaterThan(0);
        RuleFor(x => x.Reason).NotEmpty().MaximumLength(256);
    }
}
