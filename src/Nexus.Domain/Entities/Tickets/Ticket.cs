using Nexus.Domain.Common;
using Nexus.Domain.Entities.Clients;
using Nexus.Domain.Enums;

namespace Nexus.Domain.Entities.Tickets;

public class Ticket : AuditableEntity, ISoftDeletable
{
    public string Number { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string? Description { get; set; }

    public Guid? ClientId { get; set; }
    public Client? Client { get; set; }

    public string? ContactName { get; set; }
    public string? ContactEmail { get; set; }
    public string? ContactPhone { get; set; }
    public string? ContactWhatsApp { get; set; }

    public TicketStatus Status { get; set; } = TicketStatus.Open;
    public TicketPriority Priority { get; set; } = TicketPriority.Medium;
    public TicketCategory Category { get; set; } = TicketCategory.Support;

    public DateTime? SlaDeadline { get; set; }
    public DateTime? StartedAt { get; set; }
    public DateTime? ResolvedAt { get; set; }
    public DateTime? ClosedAt { get; set; }

    public string? Resolution { get; set; }
    public string? InternalNotes { get; set; }
    public decimal? ServiceValue { get; set; }

    public string? PreferredTime { get; set; }
    public bool IsFromPublic { get; set; }
    public string? TrackingToken { get; set; }

    public bool IsDeleted { get; set; }
    public DateTime? DeletedAt { get; set; }

    public ICollection<TicketComment> Comments { get; set; } = [];
    public ICollection<TicketAttachment> Attachments { get; set; } = [];
}
