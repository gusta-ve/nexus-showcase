using Nexus.Domain.Common;
using Nexus.Domain.Entities.Clients;
using Nexus.Domain.Entities.Tickets;
using Nexus.Domain.Enums;

namespace Nexus.Domain.Entities.Documents;

public class ServiceOrder : AuditableEntity, ISoftDeletable
{
    public string Number { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string? Description { get; set; }

    public Guid? ClientId { get; set; }
    public Client? Client { get; set; }

    public Guid? TicketId { get; set; }
    public Ticket? Ticket { get; set; }

    public DocumentStatus Status { get; set; } = DocumentStatus.Draft;
    public DateTime? ScheduledDate { get; set; }
    public DateTime? StartedAt { get; set; }
    public DateTime? CompletedAt { get; set; }

    public string? TechnicianNotes { get; set; }
    public string? Checklist { get; set; }

    public decimal? LaborValue { get; set; }
    public decimal? PartsValue { get; set; }
    public decimal? Discount { get; set; }
    public decimal? Total { get; set; }

    public string? ClientSignatureData { get; set; }
    public DateTime? SignedAt { get; set; }
    public string? PdfPath { get; set; }

    public bool IsDeleted { get; set; }
    public DateTime? DeletedAt { get; set; }
}
