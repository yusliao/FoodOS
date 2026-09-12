using System.Net;
using FSH.Framework.Core.Exceptions;
using FSH.Modules.Warehouse.Contracts.Dtos;
using FSH.Modules.Warehouse.Contracts.v1.Pack;
using FSH.Modules.Warehouse.Data;
using FSH.Modules.Warehouse.Domain;
using FSH.Modules.Warehouse.Features.v1;
using Mediator;
using Microsoft.EntityFrameworkCore;

namespace FSH.Modules.Warehouse.Features.v1.Pack.CreatePackTote;

public sealed class CreatePackToteCommandHandler(WarehouseDbContext dbContext)
    : ICommandHandler<CreatePackToteCommand, PackToteDto>
{
    public async ValueTask<PackToteDto> Handle(CreatePackToteCommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        var wave = await dbContext.Waves
            .FirstOrDefaultAsync(w => w.Id == command.WaveId, cancellationToken)
            .ConfigureAwait(false)
            ?? throw new NotFoundException($"Wave {command.WaveId} not found.");

        if (wave.Status != WaveStatus.Completed)
        {
            throw new CustomException(
                "Only a completed wave can be packed.",
                (IEnumerable<string>?)null,
                HttpStatusCode.Conflict);
        }

        var orderIds = command.OrderIds.Distinct().ToList();
        var waveOrders = wave.Tasks.Select(t => t.OrderId).Distinct().ToHashSet();
        if (orderIds.Exists(id => !waveOrders.Contains(id)))
        {
            throw new CustomException(
                "Pack tote orders must belong to the wave.",
                (IEnumerable<string>?)null,
                HttpStatusCode.BadRequest);
        }

        bool alreadyPacked = await dbContext.PackToteOrders
            .AnyAsync(o => orderIds.Contains(o.OrderId), cancellationToken)
            .ConfigureAwait(false);
        if (alreadyPacked)
        {
            throw new CustomException(
                "One or more orders are already packed onto a tote.",
                (IEnumerable<string>?)null,
                HttpStatusCode.Conflict);
        }

        if (command.DockLocationId is { } dockId)
        {
            var dock = await dbContext.Locations
                .FirstOrDefaultAsync(l => l.Id == dockId, cancellationToken)
                .ConfigureAwait(false)
                ?? throw new NotFoundException($"Location {dockId} not found.");
            if (dock.WarehouseId != wave.WarehouseId || dock.Type != LocationType.Dock)
            {
                throw new CustomException(
                    "Dock location must belong to this warehouse.",
                    (IEnumerable<string>?)null,
                    HttpStatusCode.BadRequest);
            }
        }

        string sscc = string.IsNullOrWhiteSpace(command.Sscc)
            ? $"SSCC{Guid.CreateVersion7():N}"[..20].ToUpperInvariant()
            : command.Sscc.Trim().ToUpperInvariant();

        var tote = PackTote.Create(wave.Id, sscc, orderIds, command.DockLocationId);
        dbContext.PackTotes.Add(tote);
        foreach (Guid orderId in tote.OrderIds)
        {
            dbContext.PackToteOrders.Add(PackToteOrder.Create(tote.Id, orderId));
        }

        await dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        return tote.ToDto(tote.OrderIds);
    }
}
