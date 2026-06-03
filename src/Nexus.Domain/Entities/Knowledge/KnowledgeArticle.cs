using Nexus.Domain.Common;

namespace Nexus.Domain.Entities.Knowledge;

public class KnowledgeArticle : AuditableEntity, ISoftDeletable
{
    public string Title { get; set; } = string.Empty;
    public string Content { get; set; } = string.Empty;
    public string? Summary { get; set; }
    public string? Category { get; set; }
    public string? Tags { get; set; }
    public string? Slug { get; set; }
    public bool IsPublic { get; set; }
    public bool IsPinned { get; set; }
    public int Views { get; set; }

    public bool IsDeleted { get; set; }
    public DateTime? DeletedAt { get; set; }
}
