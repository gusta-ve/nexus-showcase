using System.ComponentModel.DataAnnotations;
using Nexus.Domain.Enums;

namespace Nexus.Application.Features.Portal;

public class PortalDashboardDto
{
    public string ClientName { get; set; } = string.Empty;
    public int OpenTickets { get; set; }
    public int InProgressTickets { get; set; }
    public int ResolvedTickets { get; set; }
    public int TotalTickets { get; set; }
    public List<PortalTicketSummaryDto> RecentTickets { get; set; } = [];
}

public class PortalTicketSummaryDto
{
    public Guid Id { get; set; }
    public string Number { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public TicketStatus Status { get; set; }
    public TicketPriority Priority { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? ResolvedAt { get; set; }
}

public class PortalTicketDetailDto
{
    public Guid Id { get; set; }
    public string Number { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string? Description { get; set; }
    public TicketStatus Status { get; set; }
    public TicketPriority Priority { get; set; }
    public TicketCategory Category { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? StartedAt { get; set; }
    public DateTime? ResolvedAt { get; set; }
    public string? Resolution { get; set; }
    // PROIBIDO expor: InternalNotes, ServiceValue, comentários internos
    public List<PortalTicketCommentDto> Comments { get; set; } = [];
}

public class PortalTicketCommentDto
{
    public string Content { get; set; } = string.Empty;
    public bool IsFromClient { get; set; }
    public string? AuthorName { get; set; }
    public DateTime CreatedAt { get; set; }
}

public class PortalOpenTicketDto
{
    [Required(ErrorMessage = "Informe um título")]
    [MaxLength(500)]
    public string Title { get; set; } = string.Empty;

    [Required(ErrorMessage = "Descreva o problema")]
    public string Description { get; set; } = string.Empty;

    public TicketCategory Category { get; set; } = TicketCategory.Support;
    public TicketPriority Priority { get; set; } = TicketPriority.Medium;
}

public class PortalAddCommentDto
{
    [Required(ErrorMessage = "Escreva uma mensagem")]
    public string Content { get; set; } = string.Empty;
}

public class CreatePortalAccessDto
{
    public Guid ClientId { get; set; }

    [Required(ErrorMessage = "Informe o email")]
    [EmailAddress(ErrorMessage = "Email inválido")]
    public string Email { get; set; } = string.Empty;
}

public class PortalAccessCreatedDto
{
    public string Email { get; set; } = string.Empty;
    public string TemporaryPassword { get; set; } = string.Empty;
}

public class ChangePasswordDto
{
    [Required(ErrorMessage = "Informe a senha atual")]
    public string CurrentPassword { get; set; } = string.Empty;

    [Required(ErrorMessage = "Informe a nova senha")]
    [MinLength(8, ErrorMessage = "A senha deve ter ao menos 8 caracteres")]
    public string NewPassword { get; set; } = string.Empty;

    [Required(ErrorMessage = "Confirme a nova senha")]
    [Compare(nameof(NewPassword), ErrorMessage = "As senhas não conferem")]
    public string ConfirmPassword { get; set; } = string.Empty;
}
