using Nexus.Domain.Common;

namespace Nexus.Domain.Entities.Tickets;

public class TicketComment : AuditableEntity
{
    public Guid TicketId { get; set; }
    public Ticket Ticket { get; set; } = null!;

    public string Content { get; set; } = string.Empty;
    public bool IsInternal { get; set; }
    public bool IsFromClient { get; set; }
    public string? AuthorName { get; set; }
}
