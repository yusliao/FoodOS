using System.Net;
using FSH.Framework.Core.Exceptions;

namespace FSH.Modules.Catalog.Features.v1.CustomerLevels;

internal static class CustomerLevelErrors
{
    public static CustomException CodeTaken(string code) => new(
        $"A customer level with code '{code}' already exists.",
        (IEnumerable<string>?)null,
        HttpStatusCode.Conflict);
}
