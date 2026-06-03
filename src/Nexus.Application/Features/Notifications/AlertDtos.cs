using Nexus.Application.Common.Models;
using Nexus.Domain.Enums;

namespace Nexus.Application.Features.Notifications;

public class AlertListItemDto
{
    public Guid Id { get; set; }
    public AlertType Type { get; set; }
    public AlertSeverity Severity { get; set; }
    public string Title { get; set; } = string.Empty;
    public string? Message { get; set; }
    public bool IsRead { get; set; }
    public string? ActionUrl { get; set; }
    public DateTime CreatedAt { get; set; }
}

public interface IAlertService
{
    Task CreateAndDispatchAsync(
        AlertType type, AlertSeverity severity,
        string title, string? message,
        string? targetUserId = null,
        string? actionUrl = null,
        Guid? relatedEntityId = null, string? relatedEntityType = null,
        CancellationToken ct = default);

    // Roles do user determinam se ele vê broadcasts (TargetUserId == null).
    // Sem isAdmin, broadcasts são invisíveis pra esse user.
    Task<PagedResult<AlertListItemDto>> GetForUserAsync(
        string? userId, bool isAdmin, bool unreadOnly, int page, int pageSize, CancellationToken ct = default);

    Task<int> GetUnreadCountAsync(string? userId, bool isAdmin, CancellationToken ct = default);

    Task<Result> MarkReadAsync(Guid id, CancellationToken ct = default);

    Task<Result> MarkAllReadAsync(string? userId, bool isAdmin, CancellationToken ct = default);
}
