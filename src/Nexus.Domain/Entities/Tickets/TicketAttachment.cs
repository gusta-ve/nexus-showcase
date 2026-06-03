using Nexus.Domain.Common;

namespace Nexus.Domain.Entities.Tickets;

public class TicketAttachment : BaseEntity
{
    public Guid TicketId { get; set; }
    public Ticket Ticket { get; set; } = null!;

    public string FileName { get; set; } = string.Empty;
    public string FilePath { get; set; } = string.Empty;
    public string? ContentType { get; set; }
    public long FileSize { get; set; }
    public string? UploadedBy { get; set; }
}
