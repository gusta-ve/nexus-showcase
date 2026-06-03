using Nexus.Domain.Common;
using Nexus.Domain.Entities.Clients;
using Nexus.Domain.Enums;

namespace Nexus.Domain.Entities.Financial;

public class Transaction : AuditableEntity, ISoftDeletable
{
    public string Title { get; set; } = string.Empty;
    public string? Description { get; set; }
    public decimal Amount { get; set; }
    public TransactionType Type { get; set; }
    public TransactionStatus Status { get; set; } = TransactionStatus.Pending;

    public Guid? CategoryId { get; set; }
    public TransactionCategory? Category { get; set; }

    public Guid? ClientId { get; set; }
    public Client? Client { get; set; }

    public DateTime DueDate { get; set; }
    public DateTime? PaidAt { get; set; }
    public string? PaymentMethod { get; set; }

    public bool IsRecurring { get; set; }
    public RecurrenceType RecurrenceType { get; set; } = RecurrenceType.None;
    public Guid? ParentTransactionId { get; set; }

    public string? Notes { get; set; }
    public string? ReceiptPath { get; set; }

    public bool IsDeleted { get; set; }
    public DateTime? DeletedAt { get; set; }
}
