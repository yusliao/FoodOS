using FSH.Modules.Logistics.Contracts.v1.Drivers;
using FSH.Modules.Logistics.Data;
using FSH.Modules.Logistics.Domain;
using Mediator;
using Microsoft.EntityFrameworkCore;

namespace FSH.Modules.Logistics.Features.v1.Drivers.CreateDriver;

public sealed class CreateDriverCommandHandler(LogisticsDbContext dbContext)
    : ICommandHandler<CreateDriverCommand, Guid>
{
    public async ValueTask<Guid> Handle(CreateDriverCommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);
        var existing = await dbContext.Drivers
            .FirstOrDefaultAsync(d => d.UserId == command.UserId, cancellationToken)
            .ConfigureAwait(false);
        if (existing is not null)
        {
            return existing.Id;
        }

        var driver = Driver.Create(command.UserId, command.Phone);
        dbContext.Drivers.Add(driver);
        await dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        return driver.Id;
    }
}
