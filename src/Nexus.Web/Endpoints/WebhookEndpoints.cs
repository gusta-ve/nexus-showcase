using Microsoft.Extensions.Configuration;
using Nexus.Application.Features.Automation;

namespace Nexus.Web.Endpoints;

public static class WebhookEndpoints
{
    public static void MapWebhookEndpoints(this WebApplication app)
    {
        var secret = app.Configuration["Webhooks:Secret"];

        app.MapPost("/api/webhooks/{name}", async (string name, HttpContext ctx, AutomationService svc) =>
        {
            if (!string.IsNullOrEmpty(secret))
            {
                var header = ctx.Request.Headers["X-Webhook-Secret"].ToString();
                if (!string.Equals(header, secret, StringComparison.Ordinal))
                    return Results.Unauthorized();
            }
            string? payload = null;
            try
            {
                using var reader = new StreamReader(ctx.Request.Body);
                payload = await reader.ReadToEndAsync();
                if (payload.Length > 16_000)
                    payload = payload[..16_000] + "\n...[truncado]";
            }
            catch { /* body vazio ou ilegível é OK */ }

            var source = ctx.Connection.RemoteIpAddress?.ToString() ?? "unknown";
            var log = await svc.LogInboundAsync(name, source, payload);

            return Results.Ok(new { received = true, id = log.Id, name = log.Name });
        });
    }
}
