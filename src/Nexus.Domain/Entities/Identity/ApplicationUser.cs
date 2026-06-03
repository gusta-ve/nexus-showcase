using Microsoft.AspNetCore.Identity;

namespace Nexus.Domain.Entities.Identity;

public class ApplicationUser : IdentityUser
{
    public string FullName { get; set; } = string.Empty;
    public string? AvatarUrl { get; set; }
    public string? Bio { get; set; }
    public string? WhatsApp { get; set; }
    public bool IsActive { get; set; } = true;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? LastLoginAt { get; set; }

    // ── Portal do Cliente ─────────────────────────────────────────
    // Quando o usuário é um cliente do portal, ClientId aponta para
    // o registro de Client (CRM). Admins têm ClientId = null.
    public Guid? ClientId { get; set; }

    // Força o cliente a trocar a senha no primeiro acesso.
    public bool MustChangePassword { get; set; }
}
