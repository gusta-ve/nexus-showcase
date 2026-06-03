using System.ComponentModel.DataAnnotations;
using Nexus.Domain.Enums;

namespace Nexus.Application.Features.ServiceOrders;

public class ServiceOrderListItemDto
{
    public Guid Id { get; set; }
    public string Number { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string? ClientName { get; set; }
    public DocumentStatus Status { get; set; }
    public decimal? Total { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? ScheduledDate { get; set; }
    public DateTime? CompletedAt { get; set; }
}

public class ServiceOrderDetailDto
{
    public Guid Id { get; set; }
    public string Number { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string? Description { get; set; }
    public Guid? ClientId { get; set; }
    public string? ClientName { get; set; }
    public Guid? TicketId { get; set; }
    public string? TicketNumber { get; set; }
    public DocumentStatus Status { get; set; }
    public DateTime? ScheduledDate { get; set; }
    public DateTime? StartedAt { get; set; }
    public DateTime? CompletedAt { get; set; }
    public string? TechnicianNotes { get; set; }
    public string? Checklist { get; set; }
    public decimal? LaborValue { get; set; }
    public decimal? PartsValue { get; set; }
    public decimal? Discount { get; set; }
    public decimal? Total { get; set; }
    public DateTime CreatedAt { get; set; }
}

public class ServiceOrderFormDto
{
    public Guid? Id { get; set; }

    [Required(ErrorMessage = "Informe o título")]
    [MaxLength(300)]
    public string Title { get; set; } = string.Empty;

    public string? Description { get; set; }
    public Guid? ClientId { get; set; }
    public Guid? TicketId { get; set; }
    public DateTime? ScheduledDate { get; set; }

    public string? TechnicianNotes { get; set; }
    public string? Checklist { get; set; }

    [Range(0, 9999999)]
    public decimal? LaborValue { get; set; }
    [Range(0, 9999999)]
    public decimal? PartsValue { get; set; }
    [Range(0, 9999999)]
    public decimal? Discount { get; set; }
}
