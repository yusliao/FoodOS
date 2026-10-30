using FSH.Framework.Core.Exceptions;
using FSH.Modules.Catalog.Contracts.v1.PriceLists;
using FSH.Modules.Catalog.Data;
using Mediator;
using Microsoft.EntityFrameworkCore;

namespace FSH.Modules.Catalog.Features.v1.PriceLists.DeletePriceListLine;

public sealed class DeletePriceListLineCommandHandler(CatalogDbContext dbContext)
    : ICommandHandler<DeletePriceListLineCommand, Unit>
{
    public async ValueTask<Unit> Handle(DeletePriceListLineCommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        // 行必须属于路径里的价盘，防止拿别的价盘 ID 删错行
        var line = await dbContext.PriceListLines
            .FirstOrDefaultAsync(l => l.Id == command.LineId && l.PriceListId == command.PriceListId, cancellationToken)
            .ConfigureAwait(false)
            ?? throw new NotFoundException($"Price list line {command.LineId} not found.");

        dbContext.PriceListLines.Remove(line);
        await dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        return Unit.Value;
    }
}
