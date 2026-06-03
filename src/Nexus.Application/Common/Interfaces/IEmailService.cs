namespace Nexus.Application.Common.Interfaces;

/// <summary>
/// Envio de emails transacionais. Falha de envio NUNCA propaga — features
/// que dependem disso (alertas, notificações) precisam continuar funcionando
/// mesmo sem SMTP configurado.
/// </summary>
public interface IEmailService
{
    /// <summary>True se o servidor SMTP está configurado (Email__Enabled=true + host).</summary>
    bool IsEnabled { get; }

    /// <summary>Manda email pro endereço configurado em Email__AdminTo.</summary>
    Task NotifyAdminAsync(string subject, string htmlBody, CancellationToken ct = default);

    /// <summary>Manda email pra um destinatário específico.</summary>
    Task SendAsync(string to, string subject, string htmlBody, CancellationToken ct = default);
}
