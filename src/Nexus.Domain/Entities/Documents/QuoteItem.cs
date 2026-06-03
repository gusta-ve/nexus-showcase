using Nexus.Domain.Common;

namespace Nexus.Domain.Entities.Documents;

public class QuoteItem : BaseEntity
{
    public Guid QuoteId { get; set; }
    public Quote Quote { get; set; } = null!;

    public string Description { get; set; } = string.Empty;
    public decimal UnitPrice { get; set; }
    public decimal Quantity { get; set; } = 1;
    public decimal Total { get; set; }
    public int DisplayOrder { get; set; }
}
