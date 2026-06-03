using System.Net;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Nexus.Application.Features.Notifications;
using Nexus.Domain.Entities.Servers;
using Nexus.Domain.Enums;
using Nexus.Infrastructure.Persistence;

namespace Nexus.Infrastructure.Services;

/// <summary>
/// Worker que checa periodicamente o status dos servidores com MonitoringEnabled=true.
/// Estratégia: se HealthCheckUrl preenchido → HTTP GET (2xx/3xx = up).
/// Senão, se PublicIp preenchido → TCP ping na porta 22 (SSH) com timeout curto.
/// Atualiza Status + LastCheckedAt; quando muda de Online → Offline, cria Alert.
/// </summary>
public class ServerHealthCheckWorker(
    IServiceScopeFactory scopeFactory,
    ILogger<ServerHealthCheckWorker> logger,
    IConfiguration config) : BackgroundService
{
    private static readonly TimeSpan Interval = TimeSpan.FromMinutes(5);
    private static readonly TimeSpan HttpTimeout = TimeSpan.FromSeconds(8);
    private static readonly TimeSpan TcpTimeout = TimeSpan.FromSeconds(4);

    // Numa instância pública (demo, admin aberto) bloqueamos também as faixas
    // privadas, fechando o vetor de SSRF (visitante apontar o monitor pra rede
    // interna). Loopback/link-local/metadata são bloqueados SEMPRE, mesmo no prod.
    private readonly bool _blockPrivate =
        string.Equals(config["App:IsDemo"], "true", StringComparison.OrdinalIgnoreCase);

    protected override async Task ExecuteAsync(CancellationToken ct)
    {
        // Pequeno delay inicial pra app subir antes de bater no banco
        try { await Task.Delay(TimeSpan.FromSeconds(20), ct); }
        catch (TaskCanceledException) { return; }

        using var http = new HttpClient { Timeout = HttpTimeout };
        http.DefaultRequestHeaders.UserAgent.ParseAdd("Nexus-HealthCheck/1.0");

        while (!ct.IsCancellationRequested)
        {
            try
            {
                await RunCycleAsync(http, ct);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Health check cycle failed");
            }

            try { await Task.Delay(Interval, ct); }
            catch (TaskCanceledException) { return; }
        }
    }

    private async Task RunCycleAsync(HttpClient http, CancellationToken ct)
    {
        using var scope = scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<NexusDbContext>();
        var alerts = scope.ServiceProvider.GetRequiredService<IAlertService>();

        var targets = await db.Servers
            .Where(s => s.MonitoringEnabled)
            .Select(s => new { s.Id, s.HealthCheckUrl, s.PublicIp, s.SshPort, s.Status, s.Name })
            .ToListAsync(ct);

        if (targets.Count == 0) return;

        logger.LogDebug("Health check cycle: {Count} server(s)", targets.Count);

        // Coleta transições antes de salvar e depois dispara alertas
        // (IAlertService usa context próprio via factory, sem brigar com este scope).
        var transitions = new List<(string Name, Guid Id)>();

        foreach (var t in targets)
        {
            var newStatus = await ProbeAsync(http, t.HealthCheckUrl, t.PublicIp, t.SshPort, _blockPrivate, ct);

            var server = await db.Servers.FirstOrDefaultAsync(s => s.Id == t.Id, ct);
            if (server is null) continue;

            var previous = server.Status;
            server.Status = newStatus;
            server.LastCheckedAt = DateTime.UtcNow;

            if (previous == ServerStatus.Online && newStatus == ServerStatus.Offline)
            {
                transitions.Add((server.Name, server.Id));
                logger.LogWarning("Server {Name} ({Id}) is now OFFLINE", server.Name, server.Id);
            }
        }

        await db.SaveChangesAsync(ct);

        // Dispara alertas + notificação real-time pros admins logados
        foreach (var (name, id) in transitions)
        {
            await alerts.CreateAndDispatchAsync(
                type: AlertType.ServerDown,
                severity: AlertSeverity.Error,
                title: $"Servidor offline: {name}",
                message: $"Health check falhou às {DateTime.UtcNow:HH:mm} UTC",
                actionUrl: $"/servidores/{id}",
                relatedEntityId: id,
                relatedEntityType: nameof(ServerEntry),
                ct: ct);
        }
    }

    private static async Task<ServerStatus> ProbeAsync(
        HttpClient http, string? url, string? ip, string? sshPort, bool blockPrivate, CancellationToken ct)
    {
        if (!string.IsNullOrWhiteSpace(url))
        {
            if (!Uri.TryCreate(url, UriKind.Absolute, out var uri)
                || (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps)
                || await IsBlockedTargetAsync(uri.Host, blockPrivate, ct))
                return ServerStatus.Unknown;
            return await HttpProbeAsync(http, url, ct);
        }

        if (!string.IsNullOrWhiteSpace(ip))
        {
            if (await IsBlockedTargetAsync(ip, blockPrivate, ct))
                return ServerStatus.Unknown;
            return await TcpProbeAsync(ip, sshPort, ct);
        }

        return ServerStatus.Unknown;
    }

    // Anti-SSRF: resolve o host e recusa alvos perigosos. Loopback, link-local
    // (inclui 169.254.169.254, metadata de cloud) e endereços especiais são
    // sempre barrados; faixas privadas só quando blockPrivate (instância pública).
    private static async Task<bool> IsBlockedTargetAsync(string host, bool blockPrivate, CancellationToken ct)
    {
        IPAddress[] addrs;
        if (IPAddress.TryParse(host, out var literal))
            addrs = [literal];
        else
        {
            try { addrs = await Dns.GetHostAddressesAsync(host, ct); }
            catch { return true; } // não resolveu → barra por segurança
        }
        return addrs.Length == 0 || addrs.Any(a => IsBlockedAddress(a, blockPrivate));
    }

    private static bool IsBlockedAddress(IPAddress ip, bool blockPrivate)
    {
        if (IPAddress.IsLoopback(ip)) return true;
        var b = ip.GetAddressBytes();

        if (ip.AddressFamily == System.Net.Sockets.AddressFamily.InterNetwork)
        {
            if (b[0] == 0) return true;                          // 0.0.0.0/8
            if (b[0] == 169 && b[1] == 254) return true;         // link-local + metadata
            if (!blockPrivate) return false;
            if (b[0] == 10) return true;                         // 10.0.0.0/8
            if (b[0] == 172 && b[1] >= 16 && b[1] <= 31) return true; // 172.16/12
            if (b[0] == 192 && b[1] == 168) return true;         // 192.168/16
            if (b[0] == 100 && b[1] >= 64 && b[1] <= 127) return true; // CGNAT 100.64/10
            return false;
        }

        // IPv6
        if (ip.IsIPv6LinkLocal || ip.IsIPv6Multicast) return true;
        if (!blockPrivate) return false;
        if ((b[0] & 0xFE) == 0xFC) return true;                  // unique local fc00::/7
        return false;
    }

    private static async Task<ServerStatus> HttpProbeAsync(HttpClient http, string url, CancellationToken ct)
    {
        try
        {
            using var resp = await http.GetAsync(url, HttpCompletionOption.ResponseHeadersRead, ct);
            return (int)resp.StatusCode < 500 ? ServerStatus.Online : ServerStatus.Offline;
        }
        catch
        {
            return ServerStatus.Offline;
        }
    }

    private static async Task<ServerStatus> TcpProbeAsync(string host, string? portText, CancellationToken ct)
    {
        if (!int.TryParse(portText, out var port)) port = 22;
        try
        {
            using var tcp = new System.Net.Sockets.TcpClient();
            using var cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
            cts.CancelAfter(TcpTimeout);
            await tcp.ConnectAsync(host, port, cts.Token);
            return tcp.Connected ? ServerStatus.Online : ServerStatus.Offline;
        }
        catch
        {
            return ServerStatus.Offline;
        }
    }
}
