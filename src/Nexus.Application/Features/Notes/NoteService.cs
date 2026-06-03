using Microsoft.EntityFrameworkCore;
using Nexus.Application.Common.Interfaces;
using Nexus.Application.Common.Models;
using Nexus.Domain.Entities.Notes;

namespace Nexus.Application.Features.Notes;

public class NoteService(IRepository<Note> notes, IUnitOfWork uow)
{
    public async Task<PagedResult<NoteListItemDto>> GetPagedAsync(
        string? search, bool includeArchived,
        int page, int pageSize, CancellationToken ct = default)
    {
        page = page < 1 ? 1 : page;
        pageSize = pageSize is < 1 or > 200 ? 50 : pageSize;

        var q = notes.Query();
        if (!includeArchived) q = q.Where(n => !n.IsArchived);

        if (!string.IsNullOrWhiteSpace(search))
        {
            var s = search.Trim().ToLower();
            q = q.Where(n =>
                n.Title.ToLower().Contains(s) ||
                n.Content.ToLower().Contains(s) ||
                (n.Tags != null && n.Tags.ToLower().Contains(s)));
        }

        var total = await q.CountAsync(ct);

        var items = await q
            .OrderByDescending(n => n.IsPinned)
            .ThenByDescending(n => n.UpdatedAt ?? n.CreatedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(n => new NoteListItemDto
            {
                Id = n.Id,
                Title = n.Title,
                Content = n.Content,
                Tags = n.Tags,
                Color = n.Color,
                IsPinned = n.IsPinned,
                IsArchived = n.IsArchived,
                CreatedAt = n.CreatedAt,
                UpdatedAt = n.UpdatedAt
            })
            .ToListAsync(ct);

        return new PagedResult<NoteListItemDto>
        {
            Items = items, TotalCount = total, Page = page, PageSize = pageSize
        };
    }

    public async Task<NoteFormDto?> GetForEditAsync(Guid id, CancellationToken ct = default)
    {
        var n = await notes.GetByIdAsync(id, ct);
        if (n is null) return null;
        return new NoteFormDto
        {
            Id = n.Id,
            Title = n.Title,
            Content = n.Content,
            Tags = n.Tags,
            Color = n.Color,
            IsPinned = n.IsPinned
        };
    }

    public async Task<Result<Guid>> CreateAsync(NoteFormDto dto, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(dto.Title))
            return Result<Guid>.Failure("Informe o título.");

        var n = new Note();
        Apply(dto, n);
        await notes.AddAsync(n, ct);
        await uow.SaveChangesAsync(ct);
        return Result<Guid>.Success(n.Id);
    }

    public async Task<Result> UpdateAsync(Guid id, NoteFormDto dto, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(dto.Title)) return Result.Failure("Informe o título.");
        var n = await notes.GetByIdAsync(id, ct);
        if (n is null) return Result.Failure("Nota não encontrada.");
        Apply(dto, n);
        notes.Update(n);
        await uow.SaveChangesAsync(ct);
        return Result.Success();
    }

    public async Task<Result> TogglePinAsync(Guid id, CancellationToken ct = default)
    {
        var n = await notes.GetByIdAsync(id, ct);
        if (n is null) return Result.Failure("Nota não encontrada.");
        n.IsPinned = !n.IsPinned;
        notes.Update(n);
        await uow.SaveChangesAsync(ct);
        return Result.Success();
    }

    public async Task<Result> ToggleArchiveAsync(Guid id, CancellationToken ct = default)
    {
        var n = await notes.GetByIdAsync(id, ct);
        if (n is null) return Result.Failure("Nota não encontrada.");
        n.IsArchived = !n.IsArchived;
        if (n.IsArchived) n.IsPinned = false;
        notes.Update(n);
        await uow.SaveChangesAsync(ct);
        return Result.Success();
    }

    public async Task<Result> DeleteAsync(Guid id, CancellationToken ct = default)
    {
        var n = await notes.GetByIdAsync(id, ct);
        if (n is null) return Result.Failure("Nota não encontrada.");
        notes.Remove(n);
        await uow.SaveChangesAsync(ct);
        return Result.Success();
    }

    private static void Apply(NoteFormDto dto, Note n)
    {
        n.Title = dto.Title.Trim();
        n.Content = dto.Content;
        n.Tags = dto.Tags?.Trim();
        n.Color = dto.Color?.Trim();
        n.IsPinned = dto.IsPinned;
    }
}
