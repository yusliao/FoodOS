using FluentValidation;
using FSH.Modules.Inventory.Contracts.v1.Stock;

namespace FSH.Modules.Inventory.Features.v1.Stock.AllocateReservation;

public sealed class AllocateReservationCommandValidator : AbstractValidator<AllocateReservationCommand>
{
    public AllocateReservationCommandValidator()
    {
        RuleFor(x => x.ReservationId).NotEmpty();
        RuleFor(x => x.IdempotencyKey).NotEmpty().MaximumLength(128);
        RuleFor(x => x.MinRemainingDaysOnShip).GreaterThanOrEqualTo(0);
    }
}
