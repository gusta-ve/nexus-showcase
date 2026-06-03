using Nexus.Domain.Common;
using Nexus.Domain.Entities.Clients;
using Nexus.Domain.Entities.Tickets;

namespace Nexus.Domain.Entities.Reviews;

public class ClientReview : AuditableEntity
{
    public Guid? ClientId { get; set; }
    public Client? Client { get; set; }

    public Guid? TicketId { get; set; }
    public Ticket? Ticket { get; set; }

    public string ReviewerName { get; set; } = string.Empty;
    public int Rating { get; set; }
    public string? Comment { get; set; }
    public bool IsApproved { get; set; }
    public bool IsPublic { get; set; } = true;
}
