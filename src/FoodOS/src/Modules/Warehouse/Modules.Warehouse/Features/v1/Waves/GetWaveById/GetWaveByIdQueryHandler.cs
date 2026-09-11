using FSH.Framework.Core.Exceptions;
using FSH.Modules.Warehouse.Contracts.Dtos;
using FSH.Modules.Warehouse.Contracts.v1.Waves;
using FSH.Modules.Warehouse.Data;
using FSH.Modules.Warehouse.Features.v1;
using Mediator;
using Microsoft.EntityFrameworkCore;

namespace FSH.Modules.Warehouse.Features.v1.Waves.GetWaveById;

public sealed class GetWaveByIdQueryHandler(WarehouseDbContext dbContext)
    : IQueryHandler<GetWaveByIdQuery, WaveDto>
{
    public async ValueTask<WaveDto> Handle(GetWaveByIdQuery query, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);

        var wave = await dbContext.Waves
            .AsNoTracking()
            .FirstOrDefaultAsync(w => w.Id == query.WaveId, cancellationToken)
            .ConfigureAwait(false)
            ?? throw new NotFoundException($"Wave {query.WaveId} not found.");

        return wave.ToDto();
    }
}
