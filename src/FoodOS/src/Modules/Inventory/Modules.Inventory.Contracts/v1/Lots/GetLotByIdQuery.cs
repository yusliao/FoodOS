using FSH.Modules.Inventory.Contracts.Dtos;
using Mediator;

namespace FSH.Modules.Inventory.Contracts.v1.Lots;

public sealed record GetLotByIdQuery(Guid LotId) : IQuery<LotDetailDto>;
