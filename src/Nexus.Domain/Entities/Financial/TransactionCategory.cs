using Nexus.Domain.Common;
using Nexus.Domain.Enums;

namespace Nexus.Domain.Entities.Financial;

public class TransactionCategory : AuditableEntity
{
    public string Name { get; set; } = string.Empty;
    public string? Color { get; set; }
    public string? Icon { get; set; }
    public TransactionType Type { get; set; }

    public ICollection<Transaction> Transactions { get; set; } = [];
}
