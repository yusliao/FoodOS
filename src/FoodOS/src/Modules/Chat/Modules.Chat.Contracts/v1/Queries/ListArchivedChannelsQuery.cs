using FSH.Framework.Shared.Persistence;
using FSH.Modules.Chat.Contracts.v1.DTOs;
using Mediator;

namespace FSH.Modules.Chat.Contracts.v1.Queries;

public sealed record ListArchivedChannelsQuery(string? Search = null, int PageNumber = 1, int PageSize = 20)
    : IQuery<PagedResponse<ChannelDto>>;
