using Nexus.Domain.Common;

namespace Nexus.Domain.Entities.Vault;

public class VaultEntry : AuditableEntity, ISoftDeletable
{
    public string Name { get; set; } = string.Empty;
    public string? Category { get; set; }
    public string? Username { get; set; }
    public string EncryptedPassword { get; set; } = string.Empty;
    public string? Url { get; set; }
    public string? EncryptedNotes { get; set; }
    public string? Tags { get; set; }
    public string? Icon { get; set; }
    public bool IsFavorite { get; set; }
    public DateTime? LastUsedAt { get; set; }
    public DateTime? PasswordChangedAt { get; set; }

    public bool IsDeleted { get; set; }
    public DateTime? DeletedAt { get; set; }
}
