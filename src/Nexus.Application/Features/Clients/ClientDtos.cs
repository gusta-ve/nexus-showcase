using System.ComponentModel.DataAnnotations;
using Nexus.Domain.Enums;

namespace Nexus.Application.Features.Clients;

public class ClientListItemDto
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Email { get; set; }
    public string? WhatsApp { get; set; }
    public string? Phone { get; set; }
    public ClientType Type { get; set; }
    public ClientStatus Status { get; set; }
    public bool IsRecurring { get; set; }
    public decimal? MonthlyValue { get; set; }
    public DateTime CreatedAt { get; set; }
}

public class ClientFormDto
{
    public Guid? Id { get; set; }

    [Required(ErrorMessage = "Informe o nome")]
    [MaxLength(200)]
    public string Name { get; set; } = string.Empty;

    [EmailAddress(ErrorMessage = "Email inválido")]
    [MaxLength(256)]
    public string? Email { get; set; }

    [MaxLength(30)]
    public string? Phone { get; set; }

    [MaxLength(30)]
    public string? WhatsApp { get; set; }

    [MaxLength(20)]
    public string? Document { get; set; }

    public ClientType Type { get; set; } = ClientType.Individual;
    public ClientStatus Status { get; set; } = ClientStatus.Active;

    [MaxLength(200)]
    public string? CompanyName { get; set; }

    public string? Address { get; set; }
    public string? City { get; set; }
    public string? State { get; set; }
    public string? ZipCode { get; set; }
    public string? Notes { get; set; }

    [MaxLength(500)]
    public string? Tags { get; set; }

    public bool IsRecurring { get; set; }

    [Range(0, 9999999, ErrorMessage = "Valor inválido")]
    public decimal? MonthlyValue { get; set; }
}
