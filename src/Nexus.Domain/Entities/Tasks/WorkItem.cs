using Nexus.Domain.Common;
using Nexus.Domain.Enums;

namespace Nexus.Domain.Entities.Tasks;

public class WorkItem : AuditableEntity, ISoftDeletable
{
    public string Title { get; set; } = string.Empty;
    public string? Description { get; set; }
    public WorkItemPriority Priority { get; set; } = WorkItemPriority.Medium;
    public WorkItemStatus Status { get; set; } = WorkItemStatus.Todo;
    public string? Category { get; set; }
    public string? Tags { get; set; }
    public DateTime? DueDate { get; set; }
    public DateTime? CompletedAt { get; set; }
    public bool IsRecurring { get; set; }
    public Guid? RelatedClientId { get; set; }
    public Guid? RelatedTicketId { get; set; }

    public bool IsDeleted { get; set; }
    public DateTime? DeletedAt { get; set; }
}
