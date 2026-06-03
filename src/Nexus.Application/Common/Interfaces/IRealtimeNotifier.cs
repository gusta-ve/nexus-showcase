namespace Nexus.Application.Common.Interfaces;

// Lado PUBLISHER: chamado pelos services pra disparar notificação.
public interface IRealtimeNotifier
{
    Task NotifyRolesAsync(IEnumerable<string> roles, RealtimeNotification payload, CancellationToken ct = default);
    Task NotifyUserAsync(string userId, RealtimeNotification payload, CancellationToken ct = default);
}

// Lado SUBSCRIBER: o NotificationBell se registra pra receber.
// Implementação singleton in-memory no Web (Blazor Server já tem circuito WebSocket dedicado por user).
public interface IRealtimeSubscriber
{
    IDisposable Subscribe(string key, Func<RealtimeNotification, Task> handler);
}

public record RealtimeNotification(
    string Title,
    string? Message,
    string? Url,
    string Severity);   // "info" | "success" | "warning" | "error"
