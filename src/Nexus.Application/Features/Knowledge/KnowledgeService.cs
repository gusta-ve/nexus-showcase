using System.ComponentModel.DataAnnotations;
using Microsoft.EntityFrameworkCore;
using Nexus.Application.Common.Interfaces;
using Nexus.Application.Common.Models;
using Nexus.Domain.Entities.Knowledge;

namespace Nexus.Application.Features.Knowledge;

public class KnowledgeListItemDto
{
    public Guid Id { get; set; }
    public string Title { get; set; } = string.Empty;
    public string? Summary { get; set; }
    public string? Category { get; set; }
    public string? Tags { get; set; }
    public bool IsPinned { get; set; }
    public int Views { get; set; }
    public DateTime UpdatedAt { get; set; }
}

public class KnowledgeDetailDto
{
    public Guid Id { get; set; }
    public string Title { get; set; } = string.Empty;
    public string Content { get; set; } = string.Empty;
    public string? Summary { get; set; }
    public string? Category { get; set; }
    public string? Tags { get; set; }
    public bool IsPinned { get; set; }
    public int Views { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}

public class KnowledgeFormDto
{
    public Guid? Id { get; set; }

    [Required(ErrorMessage = "Informe o título")]
    [MaxLength(300)]
    public string Title { get; set; } = string.Empty;

    [Required(ErrorMessage = "Conteúdo é obrigatório")]
    public string Content { get; set; } = string.Empty;

    [MaxLength(500)]
    public string? Summary { get; set; }

    [MaxLength(80)]
    public string? Category { get; set; }

    [MaxLength(300)]
    public string? Tags { get; set; }   // CSV: "linux, ssh, hardening"

    public bool IsPinned { get; set; }
}

public class KnowledgeService(IRepository<KnowledgeArticle> repo, IUnitOfWork uow)
{
    public async Task<PagedResult<KnowledgeListItemDto>> GetPagedAsync(
        string? search, string? category, string? tag,
        int page, int pageSize, CancellationToken ct = default)
    {
        page = page < 1 ? 1 : page;
        pageSize = pageSize is < 1 or > 100 ? 20 : pageSize;

        var q = repo.Query();

        if (!string.IsNullOrWhiteSpace(category))
            q = q.Where(x => x.Category == category);

        if (!string.IsNullOrWhiteSpace(tag))
        {
            var t = tag.Trim().ToLower();
            q = q.Where(x => x.Tags != null && x.Tags.ToLower().Contains(t));
        }

        if (!string.IsNullOrWhiteSpace(search))
        {
            var s = search.Trim().ToLower();
            q = q.Where(x => x.Title.ToLower().Contains(s)
                          || (x.Summary != null && x.Summary.ToLower().Contains(s))
                          || x.Content.ToLower().Contains(s)
                          || (x.Tags != null && x.Tags.ToLower().Contains(s)));
        }

        var total = await q.CountAsync(ct);
        var list = await q
            .OrderByDescending(x => x.IsPinned)
            .ThenByDescending(x => x.UpdatedAt ?? x.CreatedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(x => new KnowledgeListItemDto
            {
                Id = x.Id,
                Title = x.Title,
                Summary = x.Summary,
                Category = x.Category,
                Tags = x.Tags,
                IsPinned = x.IsPinned,
                Views = x.Views,
                UpdatedAt = x.UpdatedAt ?? x.CreatedAt
            })
            .ToListAsync(ct);

        return new PagedResult<KnowledgeListItemDto>
        {
            Items = list, TotalCount = total, Page = page, PageSize = pageSize
        };
    }

    public async Task<List<string>> GetCategoriesAsync(CancellationToken ct = default)
    {
        return await repo.Query()
            .Where(x => x.Category != null && x.Category != "")
            .Select(x => x.Category!)
            .Distinct()
            .OrderBy(x => x)
            .ToListAsync(ct);
    }

    public async Task<KnowledgeDetailDto?> GetDetailAsync(Guid id, bool incrementViews = false, CancellationToken ct = default)
    {
        var a = await repo.GetByIdAsync(id, ct);
        if (a is null) return null;

        if (incrementViews)
        {
            a.Views++;
            repo.Update(a);
            await uow.SaveChangesAsync(ct);
        }

        return new KnowledgeDetailDto
        {
            Id = a.Id,
            Title = a.Title,
            Content = a.Content,
            Summary = a.Summary,
            Category = a.Category,
            Tags = a.Tags,
            IsPinned = a.IsPinned,
            Views = a.Views,
            CreatedAt = a.CreatedAt,
            UpdatedAt = a.UpdatedAt ?? a.CreatedAt
        };
    }

    public async Task<KnowledgeFormDto?> GetForEditAsync(Guid id, CancellationToken ct = default)
    {
        var a = await repo.GetByIdAsync(id, ct);
        if (a is null) return null;
        return new KnowledgeFormDto
        {
            Id = a.Id,
            Title = a.Title,
            Content = a.Content,
            Summary = a.Summary,
            Category = a.Category,
            Tags = a.Tags,
            IsPinned = a.IsPinned
        };
    }

    public async Task<Result<Guid>> CreateAsync(KnowledgeFormDto dto, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(dto.Title)) return Result<Guid>.Failure("Informe o título.");
        if (string.IsNullOrWhiteSpace(dto.Content)) return Result<Guid>.Failure("Conteúdo é obrigatório.");

        var a = new KnowledgeArticle();
        Apply(dto, a);
        await repo.AddAsync(a, ct);
        await uow.SaveChangesAsync(ct);
        return Result<Guid>.Success(a.Id);
    }

    public async Task<Result> UpdateAsync(Guid id, KnowledgeFormDto dto, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(dto.Title)) return Result.Failure("Informe o título.");
        if (string.IsNullOrWhiteSpace(dto.Content)) return Result.Failure("Conteúdo é obrigatório.");

        var a = await repo.GetByIdAsync(id, ct);
        if (a is null) return Result.Failure("Artigo não encontrado.");
        Apply(dto, a);
        repo.Update(a);
        await uow.SaveChangesAsync(ct);
        return Result.Success();
    }

    public async Task<Result> TogglePinAsync(Guid id, CancellationToken ct = default)
    {
        var a = await repo.GetByIdAsync(id, ct);
        if (a is null) return Result.Failure("Artigo não encontrado.");
        a.IsPinned = !a.IsPinned;
        repo.Update(a);
        await uow.SaveChangesAsync(ct);
        return Result.Success();
    }

    public async Task<Result> DeleteAsync(Guid id, CancellationToken ct = default)
    {
        var a = await repo.GetByIdAsync(id, ct);
        if (a is null) return Result.Failure("Artigo não encontrado.");
        repo.Remove(a);
        await uow.SaveChangesAsync(ct);
        return Result.Success();
    }

    private static void Apply(KnowledgeFormDto dto, KnowledgeArticle a)
    {
        a.Title = dto.Title.Trim();
        a.Content = dto.Content;            // mantém formatação (markdown/code)
        a.Summary = string.IsNullOrWhiteSpace(dto.Summary) ? null : dto.Summary.Trim();
        a.Category = string.IsNullOrWhiteSpace(dto.Category) ? null : dto.Category.Trim();
        a.Tags = NormalizeTags(dto.Tags);
        a.IsPinned = dto.IsPinned;
    }

    private static string? NormalizeTags(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw)) return null;
        var tags = raw.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(t => t.ToLowerInvariant())
            .Distinct()
            .ToArray();
        return tags.Length == 0 ? null : string.Join(", ", tags);
    }
}
