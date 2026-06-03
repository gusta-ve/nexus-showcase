using Microsoft.AspNetCore.Authorization;
using Nexus.Application.Features.Quotes;
using Nexus.Application.Features.ServiceOrders;
using Nexus.Web.Services.Pdf;

namespace Nexus.Web.Endpoints;

public static class DocumentEndpoints
{
    public static void MapDocumentEndpoints(this WebApplication app)
    {
        var group = app.MapGroup("/api/docs").RequireAuthorization(p => p.RequireRole("Admin", "Technician"));

        group.MapGet("/quotes/{id:guid}/pdf", async (Guid id, QuoteService svc) =>
        {
            var q = await svc.GetDetailAsync(id);
            if (q is null) return Results.NotFound();
            var pdf = new QuotePdfGenerator().Render(q);
            return Results.File(pdf, "application/pdf", QuotePdfGenerator.FileName(q));
        });

        group.MapGet("/service-orders/{id:guid}/pdf", async (Guid id, ServiceOrderService svc) =>
        {
            var o = await svc.GetDetailAsync(id);
            if (o is null) return Results.NotFound();
            var pdf = new ServiceOrderPdfGenerator().Render(o);
            return Results.File(pdf, "application/pdf", ServiceOrderPdfGenerator.FileName(o));
        });
    }
}
