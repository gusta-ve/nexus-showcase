using System.ComponentModel.DataAnnotations;
using Nexus.Domain.Enums;

namespace Nexus.Application.Features.Financial;

public class TransactionListItemDto
{
    public Guid Id { get; set; }
    public string Title { get; set; } = string.Empty;
    public decimal Amount { get; set; }
    public TransactionType Type { get; set; }
    public TransactionStatus Status { get; set; }
    public string? CategoryName { get; set; }
    public string? CategoryColor { get; set; }
    public string? ClientName { get; set; }
    public DateTime DueDate { get; set; }
    public DateTime? PaidAt { get; set; }
    public bool IsRecurring { get; set; }
    public RecurrenceType RecurrenceType { get; set; }
}

public class TransactionFormDto
{
    public Guid? Id { get; set; }

    [Required(ErrorMessage = "Informe o título")]
    [MaxLength(300)]
    public string Title { get; set; } = string.Empty;

    public string? Description { get; set; }

    [Range(0.01, 99999999, ErrorMessage = "Valor deve ser maior que zero")]
    public decimal Amount { get; set; }

    public TransactionType Type { get; set; } = TransactionType.Income;
    public TransactionStatus Status { get; set; } = TransactionStatus.Pending;

    public Guid? CategoryId { get; set; }
    public Guid? ClientId { get; set; }

    public DateTime DueDate { get; set; } = DateTime.UtcNow.Date;
    public DateTime? PaidAt { get; set; }
    public string? PaymentMethod { get; set; }

    public bool IsRecurring { get; set; }
    public RecurrenceType RecurrenceType { get; set; } = RecurrenceType.None;

    public string? Notes { get; set; }
}

public class CategoryDto
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Color { get; set; }
    public TransactionType Type { get; set; }
}

public class FinancialSummaryDto
{
    public decimal MonthIncome { get; set; }
    public decimal MonthExpenses { get; set; }
    public decimal MonthProfit => MonthIncome - MonthExpenses;
    public decimal PendingReceivables { get; set; }
    public decimal PendingPayables { get; set; }
    public decimal OverdueAmount { get; set; }
}
