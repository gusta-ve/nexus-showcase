using Nexus.Domain.Common;

namespace Nexus.Domain.Entities.Portfolio;

public class PortfolioImage : BaseEntity
{
    public Guid PortfolioItemId { get; set; }
    public PortfolioItem PortfolioItem { get; set; } = null!;

    public string ImageUrl { get; set; } = string.Empty;
    public string? Caption { get; set; }
    public bool IsBeforeAfter { get; set; }
    public bool IsBefore { get; set; }
    public int DisplayOrder { get; set; }
}
