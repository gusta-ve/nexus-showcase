using System.Collections.Concurrent;
using Nexus.Application.Common.Interfaces;

namespace Nexus.Web.Services;

// Singleton in-memory. Cada componente Blazor que quer escutar (ex: NotificationBell)
// chama Subscribe("user:<id>") + Subscribe("role:Admin") no OnInitializedAsync e
// retorna o IDisposable pra Dispose ao sair. Quando alguém Notify, todos os
// handlers do(s) bucket(s) são invocados.
public class InMemoryRealtimeNotifier : IRealtimeNotifier, IRealtimeSubscriber
{
    private readonly ConcurrentDictionary<string, List<Func<RealtimeNotification, Task>>> _handlers = new();

    public IDisposable Subscribe(string key, Func<RealtimeNotification, Task> handler)
    {
        var bucket = _handlers.GetOrAdd(key, _ => new List<Func<RealtimeNotification, Task>>());
        lock (bucket) bucket.Add(handler);
        return new Subscription(() =>
        {
            if (_handlers.TryGetValue(key, out var b))
                lock (b) b.Remove(handler);
        });
    }

    public Task NotifyRolesAsync(IEnumerable<string> roles, RealtimeNotification payload, CancellationToken ct = default)
        => DispatchAsync(roles.Select(r => $"role:{r}"), payload);

    public Task NotifyUserAsync(string userId, RealtimeNotification payload, CancellationToken ct = default)
        => DispatchAsync(new[] { $"user:{userId}" }, payload);

    private async Task DispatchAsync(IEnumerable<string> keys, RealtimeNotification payload)
    {
        foreach (var key in keys)
        {
            if (!_handlers.TryGetValue(key, out var bucket)) continue;
            List<Func<RealtimeNotification, Task>> snapshot;
            lock (bucket) snapshot = bucket.ToList();
            foreach (var h in snapshot)
            {
                try { await h(payload); }
                catch { /* handler quebrou — ignora pra não derrubar o dispatch */ }
            }
        }
    }

    private sealed class Subscription(Action onDispose) : IDisposable
    {
        private bool _disposed;
        public void Dispose() { if (!_disposed) { _disposed = true; onDispose(); } }
    }
}
