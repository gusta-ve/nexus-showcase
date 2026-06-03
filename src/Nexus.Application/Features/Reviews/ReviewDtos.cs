using System.ComponentModel.DataAnnotations;

namespace Nexus.Application.Features.Reviews;

public class ReviewListItemDto
{
    public Guid Id { get; set; }
    public string ReviewerName { get; set; } = string.Empty;
    public string? ClientName { get; set; }
    public string? TicketNumber { get; set; }
    public int Rating { get; set; }
    public string? Comment { get; set; }
    public bool IsApproved { get; set; }
    public bool IsPublic { get; set; }
    public DateTime CreatedAt { get; set; }
}

/// <summary>Card público pra landing — só os campos que o visitante pode ver.</summary>
public class PublicReviewDto
{
    public int Rating { get; set; }
    public string Comment { get; set; } = string.Empty;
    public string ReviewerName { get; set; } = string.Empty;
    public string? Where { get; set; }   // bairro/cidade derivado do cliente
    public DateTime CreatedAt { get; set; }
}

public class CreateReviewDto
{
    public Guid? TicketId { get; set; }

    [Range(1, 5, ErrorMessage = "Avaliação de 1 a 5 estrelas")]
    public int Rating { get; set; }

    [MaxLength(1000)]
    public string? Comment { get; set; }

    /// <summary>Se true, o admin pode publicar na landing (cliente autoriza).</summary>
    public bool AllowPublic { get; set; } = true;
}

public class ReviewStatsDto
{
    public int Total { get; set; }
    public double Average { get; set; }
    public int Count5 { get; set; }
    public int Count4 { get; set; }
    public int Count3 { get; set; }
    public int Count2 { get; set; }
    public int Count1 { get; set; }
}
