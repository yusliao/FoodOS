using FSH.Framework.Shared.Identity.Authorization;
using FSH.Modules.Billing.Contracts.Authorization;
using Mediator;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace FSH.Modules.Billing.Features.v1.Invoices.GetInvoicePdf;

public static class GetInvoicePdfEndpoint
{
    internal static RouteHandlerBuilder MapGetInvoicePdfEndpoint(this IEndpointRouteBuilder endpoints)
    {
        return endpoints.MapGet("/invoices/{invoiceId:guid}/pdf",
                async (Guid invoiceId, IMediator mediator, CancellationToken ct) =>
                {
                    var result = await mediator.Send(new GetInvoicePdfQuery(invoiceId), ct).ConfigureAwait(false);
                    return Results.File(result.Content, "application/pdf", result.FileName);
                })
            .WithName("GetInvoicePdf")
            .WithSummary("Download an invoice as a PDF")
            // Software billing is operator-only: Billing.View is not a customer-grantable permission.
            .RequirePermission(BillingPermissions.View)
            .Produces(StatusCodes.Status200OK, contentType: "application/pdf")
            .Produces(StatusCodes.Status404NotFound);
    }
}
