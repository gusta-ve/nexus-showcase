using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using Nexus.Application.Common.Interfaces;
using Nexus.Application.Features.Portal;
using Nexus.Application.Features.Quotes;
using Nexus.Application.Features.ServiceOrders;
using Nexus.Web.Services.Pdf;

namespace Nexus.Web.Endpoints;

// PDFs do portal: cliente só baixa documento DELE. Faz dupla checagem
// (portal service filtra por ClientId, e depois reutiliza o detail completo do
// service admin pra gerar o PDF — assim o layout do PDF fica idêntico ao do admin).
public static class PortalDocumentEndpoints
{
    public static void MapPortalDocumentEndpoints(this WebApplication app)
    {
        var group = app.MapGroup("/api/portal/docs")
            .RequireAuthorization(p => p.RequireRole("Client"));

        group.MapGet("/quotes/{id:guid}/pdf", async (
            Guid id, HttpContext http,
            IPortalAccessService access, PortalQuoteService portalSvc, QuoteService adminSvc) =>
        {
            var userId = http.User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (userId is null) return Results.Unauthorized();

            var ctx = await access.GetUserContextAsync(userId);
            if (ctx?.ClientId is null) return Results.Forbid();

            // valida que o quote é desse cliente
            var portal = await portalSvc.GetDetailAsync(id, ctx.ClientId.Value);
            if (portal is null) return Results.NotFound();

            // gera o PDF a partir do detail completo (reuso do layout brand)
            var full = await adminSvc.GetDetailAsync(id);
            if (full is null) return Results.NotFound();

            var pdf = new QuotePdfGenerator().Render(full);
            return Results.File(pdf, "application/pdf", QuotePdfGenerator.FileName(full));
        });

        group.MapGet("/service-orders/{id:guid}/pdf", async (
            Guid id, HttpContext http,
            IPortalAccessService access, PortalServiceOrderService portalSvc, ServiceOrderService adminSvc) =>
        {
            var userId = http.User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (userId is null) return Results.Unauthorized();

            var ctx = await access.GetUserContextAsync(userId);
            if (ctx?.ClientId is null) return Results.Forbid();

            var portal = await portalSvc.GetDetailAsync(id, ctx.ClientId.Value);
            if (portal is null) return Results.NotFound();

            var full = await adminSvc.GetDetailAsync(id);
            if (full is null) return Results.NotFound();

            var pdf = new ServiceOrderPdfGenerator().Render(full);
            return Results.File(pdf, "application/pdf", ServiceOrderPdfGenerator.FileName(full));
        });
    }
}
