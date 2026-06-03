using Nexus.Domain.Common;

namespace Nexus.Domain.Entities.Portfolio;

public class PortfolioItem : AuditableEntity, ISoftDeletable
{
    public string Title { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string? Category { get; set; }
    public string? Tags { get; set; }
    public string? ThumbnailUrl { get; set; }
    public string? ProjectUrl { get; set; }
    public string? GithubUrl { get; set; }
    public bool IsFeatured { get; set; }
    public bool IsPublished { get; set; }
    public DateTime? PublishedAt { get; set; }
    public int DisplayOrder { get; set; }

    public bool IsDeleted { get; set; }
    public DateTime? DeletedAt { get; set; }

    public ICollection<PortfolioImage> Images { get; set; } = [];
}
