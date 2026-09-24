using System.Net;
using FSH.Framework.Core.Exceptions;
using FSH.Modules.Tickets.Data;
using Microsoft.EntityFrameworkCore;

namespace FSH.Modules.Tickets.Features.v1.Internal;

internal static class TicketPersistence
{
    public static async Task SaveChangesAsync(
        TicketsDbContext dbContext,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(dbContext);

        try
        {
            await dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        }
        catch (DbUpdateConcurrencyException exception)
        {
            throw new CustomException(
                "The ticket changed while you were editing it. Reload before retrying.",
                exception,
                HttpStatusCode.Conflict);
        }
    }
}
