using System.ComponentModel.DataAnnotations;

namespace Nexus.Application.Features.Vault;

public class VaultEntryListItemDto
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Category { get; set; }
    public string? Username { get; set; }
    public string? Url { get; set; }
    public string? Tags { get; set; }
    public bool IsFavorite { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? LastUsedAt { get; set; }
    public DateTime? PasswordChangedAt { get; set; }
    // Senha NUNCA vem na lista por segurança.
}

public class VaultEntryFormDto
{
    public Guid? Id { get; set; }

    [Required(ErrorMessage = "Informe o nome")]
    [MaxLength(200)]
    public string Name { get; set; } = string.Empty;

    [MaxLength(100)]
    public string? Category { get; set; }

    [MaxLength(200)]
    public string? Username { get; set; }

    /// <summary>
    /// Em edição vem vazio (não carregamos a senha de volta).
    /// Só sobrescreve quando o usuário digita uma nova.
    /// </summary>
    public string? NewPassword { get; set; }

    public string? Url { get; set; }
    public string? Notes { get; set; }

    [MaxLength(500)]
    public string? Tags { get; set; }

    public bool IsFavorite { get; set; }
}

public class RevealedSecretDto
{
    public string Name { get; set; } = string.Empty;
    public string? Username { get; set; }
    public string Password { get; set; } = string.Empty;
    public string? Notes { get; set; }
    public string? Url { get; set; }
}
