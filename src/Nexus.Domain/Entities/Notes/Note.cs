using Nexus.Domain.Common;

namespace Nexus.Domain.Entities.Notes;

public class Note : AuditableEntity, ISoftDeletable
{
    public string Title { get; set; } = string.Empty;
    public string Content { get; set; } = string.Empty;
    public string? Tags { get; set; }
    public string? Color { get; set; }
    public bool IsPinned { get; set; }
    public bool IsArchived { get; set; }

    public bool IsDeleted { get; set; }
    public DateTime? DeletedAt { get; set; }
}
