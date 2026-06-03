using System.Net;
using System.Net.Mail;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Nexus.Application.Common.Interfaces;

namespace Nexus.Infrastructure.Services;

/// <summary>
/// Envio via SMTP padrão (Gmail, Outlook, SendGrid SMTP relay, etc).
/// Config via .env / appsettings (prefixo Email__).
/// Falha SILENCIOSA: loga mas não joga exceção — features que chamam
/// precisam continuar mesmo se SMTP cair.
/// </summary>
public class SmtpEmailService(IConfiguration config, ILogger<SmtpEmailService> logger) : IEmailService
{
    private readonly bool _enabled = config.GetValue("Email:Enabled", false);
    private readonly string? _from = config["Email:From"];
    private readonly string? _adminTo = config["Email:AdminTo"];
    private readonly string? _host = config["Email:SmtpHost"];
    private readonly int _port = config.GetValue("Email:SmtpPort", 587);
    private readonly string? _user = config["Email:SmtpUser"];
    private readonly string? _pass = config["Email:SmtpPass"];
    private readonly bool _useSsl = config.GetValue("Email:UseSsl", true);

    public bool IsEnabled => _enabled
        && !string.IsNullOrWhiteSpace(_host)
        && !string.IsNullOrWhiteSpace(_from)
        && !string.IsNullOrWhiteSpace(_adminTo);

    public Task NotifyAdminAsync(string subject, string htmlBody, CancellationToken ct = default)
        => SendAsync(_adminTo ?? "", subject, htmlBody, ct);

    public async Task SendAsync(string to, string subject, string htmlBody, CancellationToken ct = default)
    {
        if (!IsEnabled)
        {
            logger.LogDebug("Email service desabilitado — pulando '{Subject}'", subject);
            return;
        }
        if (string.IsNullOrWhiteSpace(to))
        {
            logger.LogWarning("Email destinatário vazio — pulando '{Subject}'", subject);
            return;
        }

        try
        {
            using var client = new SmtpClient(_host!, _port)
            {
                EnableSsl = _useSsl,
                Credentials = new NetworkCredential(_user, _pass)
            };
            using var msg = new MailMessage
            {
                From = new MailAddress(_from!),
                Subject = subject,
                Body = WrapInTemplate(subject, htmlBody),
                IsBodyHtml = true
            };
            msg.To.Add(to);

            await client.SendMailAsync(msg, ct);
            logger.LogInformation("Email enviado pra {To}: '{Subject}'", to, subject);
        }
        catch (Exception ex)
        {
            // NUNCA propaga — falha de email é problema secundário
            logger.LogError(ex, "Falha enviando email '{Subject}' pra {To}", subject, to);
        }
    }

    /// <summary>Envolve o corpo num template HTML com branding Nexus/Gustavo Almeida.</summary>
    private static string WrapInTemplate(string subject, string innerHtml)
    {
        return $$"""
        <!DOCTYPE html>
        <html lang="pt-BR">
        <head>
          <meta charset="utf-8">
          <meta name="viewport" content="width=device-width,initial-scale=1">
          <title>{{subject}}</title>
        </head>
        <body style="margin:0;padding:0;background:#050608;font-family:-apple-system,BlinkMacSystemFont,'Segoe UI',Inter,sans-serif;color:#e2e8f0;">
          <table role="presentation" width="100%" cellpadding="0" cellspacing="0" style="background:#050608;padding:32px 12px;">
            <tr><td align="center">
              <table role="presentation" width="100%" style="max-width:560px;background:#0a0b0e;border:1px solid #1f2937;border-radius:12px;overflow:hidden;">

                <tr><td style="padding:24px 28px;border-bottom:1px solid #1f2937;">
                  <table role="presentation" cellpadding="0" cellspacing="0"><tr>
                    <td style="vertical-align:middle;">
                      <div style="display:inline-block;width:36px;height:36px;border:2px solid #00c2ff;transform:rotate(30deg);border-radius:4px;text-align:center;">
                        <span style="display:inline-block;transform:rotate(-30deg);color:#00c2ff;font-weight:800;font-size:20px;line-height:34px;font-family:Georgia,serif;">G</span>
                      </div>
                    </td>
                    <td style="vertical-align:middle;padding-left:14px;">
                      <div style="color:#fff;font-weight:700;font-size:16px;">Gustavo Almeida · Suporte TI</div>
                      <div style="color:#00c2ff;font-size:11px;letter-spacing:0.18em;text-transform:uppercase;margin-top:2px;">Nexus · Operações</div>
                    </td>
                  </tr></table>
                </td></tr>

                <tr><td style="padding:28px;color:#cbd5e1;font-size:15px;line-height:1.6;">
                  {{innerHtml}}
                </td></tr>

                <tr><td style="padding:18px 28px;background:rgba(255,255,255,0.02);border-top:1px solid #1f2937;font-size:12px;color:#5a6a75;">
                  Esta é uma notificação automática do <a href="https://gustavoti.com" style="color:#00c2ff;text-decoration:none;">Nexus</a>.
                  Você está recebendo porque é admin do sistema.
                </td></tr>

              </table>
            </td></tr>
          </table>
        </body>
        </html>
        """;
    }
}
