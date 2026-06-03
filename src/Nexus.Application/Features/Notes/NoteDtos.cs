using System.ComponentModel.DataAnnotations;

namespace Nexus.Application.Features.Notes;

public class NoteListItemDto
{
    public Guid Id { get; set; }
    public string Title { get; set; } = string.Empty;
    public string Content { get; set; } = string.Empty;
    public string? Tags { get; set; }
    public string? Color { get; set; }
    public bool IsPinned { get; set; }
    public bool IsArchived { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }
}

public class NoteFormDto
{
    public Guid? Id { get; set; }

    [Required(ErrorMessage = "Informe o título")]
    [MaxLength(300)]
    public string Title { get; set; } = string.Empty;

    public string Content { get; set; } = string.Empty;

    [MaxLength(500)]
    public string? Tags { get; set; }

    [MaxLength(20)]
    public string? Color { get; set; }

    public bool IsPinned { get; set; }
}
