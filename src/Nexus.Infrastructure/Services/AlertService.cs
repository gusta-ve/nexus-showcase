using Microsoft.EntityFrameworkCore;
using Nexus.Application.Common.Interfaces;
using Nexus.Application.Common.Models;
using Nexus.Application.Features.Notifications;
using Nexus.Domain.Entities.Alerts;
using Nexus.Domain.Enums;
using Nexus.Infrastructure.Persistence;

namespace Nexus.Infrastructure.Services;

// Usa IDbContextFactory<NexusDbContext> em vez de IRepository: o NotificationBell
// renderiza no LAYOUT em paralelo com cada PAGE, então scoped DbContext explode.
// Factory cria um context isolado por chamada.
public class AlertService(
    IDbContextFactory<NexusDbContext> dbFactory,
    IRealtimeNotifier notifier,
    IEmailService email) : IAlertService
{
    // Eventos que disparam email pro admin além do push real-time:
    // - Server down (sempre, é incidente)
    // - Cliente abriu novo chamado (manda email se admin não tá com a tela aberta)
    private static bool ShouldEmailAdmin(AlertType type, AlertSeverity sev) =>
        type == AlertType.ServerDown
        || (type == AlertType.ClientWaiting && sev >= AlertSeverity.Info);

    public async Task CreateAndDispatchAsync(
        AlertType type, AlertSeverity severity,
        string title, string? message,
        string? targetUserId = null,
        string? actionUrl = null,
        Guid? relatedEntityId = null, string? relatedEntityType = null,
        CancellationToken ct = default)
    {
        await using var db = await dbFactory.CreateDbContextAsync(ct);
        var alert = new Alert
        {
            Type = type,
            Severity = severity,
            Title = title,
            Message = message,
            ActionUrl = actionUrl,
            RelatedEntityId = relatedEntityId,
            RelatedEntityType = relatedEntityType,
            TargetUserId = targetUserId,
            CreatedAt = DateTime.UtcNow
        };
        db.Set<Alert>().Add(alert);
        await db.SaveChangesAsync(ct);

        var payload = new RealtimeNotification(
            Title: title,
            Message: message,
            Url: actionUrl,
            Severity: severity switch
            {
                AlertSeverity.Critical => "error",
                AlertSeverity.Error => "error",
                AlertSeverity.Warning => "warning",
                _ => "info"
            });

        if (targetUserId is not null)
            await notifier.NotifyUserAsync(targetUserId, payload, ct);
        else
            await notifier.NotifyRolesAsync(new[] { "Admin", "Technician" }, payload, ct);

        // Email pra eventos críticos (broadcast pro admin).
        // Falha não derruba o fluxo (service já trata internamente).
        if (targetUserId is null && ShouldEmailAdmin(type, severity))
        {
            var body = BuildEmailBody(title, message, actionUrl, severity);
            await email.NotifyAdminAsync(title, body, ct);
        }
    }

    private static string BuildEmailBody(string title, string? message, string? actionUrl, AlertSeverity severity)
    {
        var color = severity switch
        {
            AlertSeverity.Critical or AlertSeverity.Error => "#ef4444",
            AlertSeverity.Warning => "#f59e0b",
            _ => "#00c2ff"
        };
        var html = $@"<div style=""border-left:3px solid {color};padding-left:14px;margin-bottom:14px;"">
            <div style=""color:{color};font-size:12px;letter-spacing:0.1em;text-transform:uppercase;font-weight:700;margin-bottom:6px;"">{severity}</div>
            <div style=""color:#fff;font-size:18px;font-weight:700;margin-bottom:8px;"">{title}</div>";
        if (!string.IsNullOrWhiteSpace(message))
            html += $@"<div style=""color:#cbd5e1;font-size:14px;line-height:1.5;"">{message}</div>";
        html += "</div>";

        if (!string.IsNullOrWhiteSpace(actionUrl))
        {
            var fullUrl = actionUrl.StartsWith("http") ? actionUrl : $"https://gustavoti.com{actionUrl}";
            html += $@"<div style=""margin-top:20px;""><a href=""{fullUrl}"" style=""display:inline-block;background:#00c2ff;color:#050608;text-decoration:none;font-weight:700;padding:12px 24px;border-radius:6px;"">Ver no painel</a></div>";
        }
        return html;
    }

    public async Task<PagedResult<AlertListItemDto>> GetForUserAsync(
        string? userId, bool isAdmin, bool unreadOnly, int page, int pageSize, CancellationToken ct = default)
    {
        page = page < 1 ? 1 : page;
        pageSize = pageSize is < 1 or > 100 ? 20 : pageSize;

        await using var db = await dbFactory.CreateDbContextAsync(ct);
        var q = db.Set<Alert>().AsQueryable();
        // broadcasts (TargetUserId == null) só pra admin/technician.
        // user específico SEMPRE vê o que é dele.
        q = isAdmin
            ? q.Where(a => a.TargetUserId == userId || a.TargetUserId == null)
            : q.Where(a => a.TargetUserId == userId);
        if (unreadOnly) q = q.Where(a => !a.IsRead);

        var total = await q.CountAsync(ct);
        var list = await q
            .OrderByDescending(a => a.CreatedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(a => new AlertListItemDto
            {
                Id = a.Id,
                Type = a.Type,
                Severity = a.Severity,
                Title = a.Title,
                Message = a.Message,
                IsRead = a.IsRead,
                ActionUrl = a.ActionUrl,
                CreatedAt = a.CreatedAt
            })
            .ToListAsync(ct);

        return new PagedResult<AlertListItemDto>
        {
            Items = list, TotalCount = total, Page = page, PageSize = pageSize
        };
    }

    public async Task<int> GetUnreadCountAsync(string? userId, bool isAdmin, CancellationToken ct = default)
    {
        await using var db = await dbFactory.CreateDbContextAsync(ct);
        var q = db.Set<Alert>().Where(a => !a.IsRead);
        q = isAdmin
            ? q.Where(a => a.TargetUserId == userId || a.TargetUserId == null)
            : q.Where(a => a.TargetUserId == userId);
        return await q.CountAsync(ct);
    }

    public async Task<Result> MarkReadAsync(Guid id, CancellationToken ct = default)
    {
        await using var db = await dbFactory.CreateDbContextAsync(ct);
        var a = await db.Set<Alert>().FirstOrDefaultAsync(x => x.Id == id, ct);
        if (a is null) return Result.Failure("Alerta não encontrado.");
        if (!a.IsRead)
        {
            a.IsRead = true;
            a.ReadAt = DateTime.UtcNow;
            await db.SaveChangesAsync(ct);
        }
        return Result.Success();
    }

    public async Task<Result> MarkAllReadAsync(string? userId, bool isAdmin, CancellationToken ct = default)
    {
        await using var db = await dbFactory.CreateDbContextAsync(ct);
        var q = db.Set<Alert>().Where(a => !a.IsRead);
        q = isAdmin
            ? q.Where(a => a.TargetUserId == userId || a.TargetUserId == null)
            : q.Where(a => a.TargetUserId == userId);
        var unread = await q.ToListAsync(ct);
        var now = DateTime.UtcNow;
        foreach (var a in unread)
        {
            a.IsRead = true;
            a.ReadAt = now;
        }
        await db.SaveChangesAsync(ct);
        return Result.Success();
    }
}
