using System.ComponentModel.DataAnnotations;
using Nexus.Domain.Enums;

namespace Nexus.Application.Features.Quotes;

public class QuoteListItemDto
{
    public Guid Id { get; set; }
    public string Number { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string? ClientName { get; set; }
    public DocumentStatus Status { get; set; }
    public decimal Total { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? ValidUntil { get; set; }
    public DateTime? SentAt { get; set; }
}

public class QuoteDetailDto
{
    public Guid Id { get; set; }
    public string Number { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string? Description { get; set; }
    public Guid? ClientId { get; set; }
    public string? ClientName { get; set; }
    public string? ClientEmail { get; set; }
    public string? ClientWhatsApp { get; set; }
    public DocumentStatus Status { get; set; }
    public DateTime? ValidUntil { get; set; }
    public DateTime? SentAt { get; set; }
    public DateTime? AcceptedAt { get; set; }
    public decimal Subtotal { get; set; }
    public decimal Discount { get; set; }
    public decimal Tax { get; set; }
    public decimal Total { get; set; }
    public string? Notes { get; set; }
    public string? Terms { get; set; }
    public DateTime CreatedAt { get; set; }
    public List<QuoteItemDto> Items { get; set; } = [];
}

public class QuoteItemDto
{
    public Guid Id { get; set; }
    public string Description { get; set; } = string.Empty;
    public decimal UnitPrice { get; set; }
    public decimal Quantity { get; set; }
    public decimal Total { get; set; }
    public int DisplayOrder { get; set; }
}

public class QuoteFormDto
{
    public Guid? Id { get; set; }

    [Required(ErrorMessage = "Informe o título")]
    [MaxLength(300)]
    public string Title { get; set; } = string.Empty;

    public string? Description { get; set; }

    public Guid? ClientId { get; set; }
    public string? ClientNameOverride { get; set; }

    public DateTime? ValidUntil { get; set; }

    [Range(0, 9999999)]
    public decimal Discount { get; set; }

    [Range(0, 9999999)]
    public decimal Tax { get; set; }

    public string? Notes { get; set; }
    public string? Terms { get; set; }
}

public class QuoteItemFormDto
{
    public Guid? Id { get; set; }

    [Required(ErrorMessage = "Descreva o item")]
    [MaxLength(500)]
    public string Description { get; set; } = string.Empty;

    [Range(0.01, 9999999, ErrorMessage = "Valor maior que zero")]
    public decimal UnitPrice { get; set; } = 0;

    [Range(0.01, 9999, ErrorMessage = "Quantidade maior que zero")]
    public decimal Quantity { get; set; } = 1;
}
