using System.ComponentModel.DataAnnotations;
using Nexus.Domain.Enums;

namespace Nexus.Application.Features.Tickets;

public class TicketListItemDto
{
    public Guid Id { get; set; }
    public string Number { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string? ClientName { get; set; }
    public Guid? ClientId { get; set; }
    public string? ContactName { get; set; }
    public TicketStatus Status { get; set; }
    public TicketPriority Priority { get; set; }
    public TicketCategory Category { get; set; }
    public bool IsFromPublic { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? ResolvedAt { get; set; }
}

public class TicketDetailDto
{
    public Guid Id { get; set; }
    public string Number { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string? Description { get; set; }

    public Guid? ClientId { get; set; }
    public string? ClientName { get; set; }
    public string? ContactName { get; set; }
    public string? ContactEmail { get; set; }
    public string? ContactPhone { get; set; }
    public string? ContactWhatsApp { get; set; }
    public string? PreferredTime { get; set; }

    public TicketStatus Status { get; set; }
    public TicketPriority Priority { get; set; }
    public TicketCategory Category { get; set; }

    public DateTime CreatedAt { get; set; }
    public DateTime? StartedAt { get; set; }
    public DateTime? ResolvedAt { get; set; }
    public DateTime? ClosedAt { get; set; }
    public DateTime? SlaDeadline { get; set; }

    public string? Resolution { get; set; }
    public string? InternalNotes { get; set; }
    public decimal? ServiceValue { get; set; }
    public bool IsFromPublic { get; set; }

    public List<TicketCommentDto> Comments { get; set; } = [];
}

public class TicketCommentDto
{
    public Guid Id { get; set; }
    public string Content { get; set; } = string.Empty;
    public bool IsInternal { get; set; }
    public bool IsFromClient { get; set; }
    public string? AuthorName { get; set; }
    public DateTime CreatedAt { get; set; }
}

public class TicketFormDto
{
    public Guid? Id { get; set; }

    [Required(ErrorMessage = "Informe o título")]
    [MaxLength(500)]
    public string Title { get; set; } = string.Empty;

    public string? Description { get; set; }

    public Guid? ClientId { get; set; }
    public string? ContactName { get; set; }
    public string? ContactEmail { get; set; }
    public string? ContactPhone { get; set; }
    public string? ContactWhatsApp { get; set; }
    public string? PreferredTime { get; set; }

    public TicketPriority Priority { get; set; } = TicketPriority.Medium;
    public TicketCategory Category { get; set; } = TicketCategory.Support;

    public string? InternalNotes { get; set; }

    [Range(0, 9999999)]
    public decimal? ServiceValue { get; set; }
}

public class AddCommentDto
{
    [Required(ErrorMessage = "Escreva um comentário")]
    public string Content { get; set; } = string.Empty;
    public bool IsInternal { get; set; }
}

public class ResolveTicketDto
{
    public string? Resolution { get; set; }
    public decimal? ServiceValue { get; set; }
}
