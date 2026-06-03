using Nexus.Domain.Common;
using Nexus.Domain.Entities.Clients;
using Nexus.Domain.Enums;

namespace Nexus.Domain.Entities.Documents;

public class Quote : AuditableEntity, ISoftDeletable
{
    public string Number { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string? Description { get; set; }

    public Guid? ClientId { get; set; }
    public Client? Client { get; set; }
    public string? ClientName { get; set; }

    public DocumentStatus Status { get; set; } = DocumentStatus.Draft;
    public DateTime? ValidUntil { get; set; }
    public DateTime? SentAt { get; set; }
    public DateTime? AcceptedAt { get; set; }

    public decimal Subtotal { get; set; }
    public decimal Discount { get; set; }
    public decimal Tax { get; set; }
    public decimal Total { get; set; }

    public string? Notes { get; set; }
    public string? Terms { get; set; }
    public string? PdfPath { get; set; }

    public bool IsDeleted { get; set; }
    public DateTime? DeletedAt { get; set; }

    public ICollection<QuoteItem> Items { get; set; } = [];
}
