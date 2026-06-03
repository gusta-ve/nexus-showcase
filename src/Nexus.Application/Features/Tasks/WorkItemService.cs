using Microsoft.EntityFrameworkCore;
using Nexus.Application.Common.Interfaces;
using Nexus.Application.Common.Models;
using Nexus.Domain.Entities.Tasks;
using Nexus.Domain.Enums;

namespace Nexus.Application.Features.Tasks;

public class WorkItemService(IRepository<WorkItem> tasks, IUnitOfWork uow)
{
    public async Task<PagedResult<WorkItemListItemDto>> GetPagedAsync(
        string? search,
        IReadOnlyList<WorkItemStatus>? statuses,
        IReadOnlyList<WorkItemPriority>? priorities,
        int page, int pageSize,
        CancellationToken ct = default)
    {
        page = page < 1 ? 1 : page;
        pageSize = pageSize is < 1 or > 200 ? 50 : pageSize;

        var q = tasks.Query();

        if (statuses is { Count: > 0 }) q = q.Where(t => statuses.Contains(t.Status));
        if (priorities is { Count: > 0 }) q = q.Where(t => priorities.Contains(t.Priority));

        if (!string.IsNullOrWhiteSpace(search))
        {
            var s = search.Trim().ToLower();
            q = q.Where(t =>
                t.Title.ToLower().Contains(s) ||
                (t.Tags != null && t.Tags.ToLower().Contains(s)) ||
                (t.Category != null && t.Category.ToLower().Contains(s)));
        }

        var total = await q.CountAsync(ct);

        var items = await q
            .OrderBy(t => t.Status == WorkItemStatus.Done || t.Status == WorkItemStatus.Cancelled ? 1 : 0)
            .ThenByDescending(t => t.Priority)
            .ThenBy(t => t.DueDate == null)
            .ThenBy(t => t.DueDate)
            .ThenByDescending(t => t.CreatedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(t => new WorkItemListItemDto
            {
                Id = t.Id,
                Title = t.Title,
                Description = t.Description,
                Priority = t.Priority,
                Status = t.Status,
                Category = t.Category,
                Tags = t.Tags,
                DueDate = t.DueDate,
                CompletedAt = t.CompletedAt,
                CreatedAt = t.CreatedAt
            })
            .ToListAsync(ct);

        return new PagedResult<WorkItemListItemDto>
        {
            Items = items, TotalCount = total, Page = page, PageSize = pageSize
        };
    }

    public async Task<WorkItemFormDto?> GetForEditAsync(Guid id, CancellationToken ct = default)
    {
        var t = await tasks.GetByIdAsync(id, ct);
        if (t is null) return null;
        return new WorkItemFormDto
        {
            Id = t.Id,
            Title = t.Title,
            Description = t.Description,
            Priority = t.Priority,
            Status = t.Status,
            Category = t.Category,
            Tags = t.Tags,
            DueDate = t.DueDate
        };
    }

    public async Task<Result<Guid>> CreateAsync(WorkItemFormDto dto, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(dto.Title))
            return Result<Guid>.Failure("Informe o título.");

        var t = new WorkItem();
        Apply(dto, t);
        await tasks.AddAsync(t, ct);
        await uow.SaveChangesAsync(ct);
        return Result<Guid>.Success(t.Id);
    }

    public async Task<Result> UpdateAsync(Guid id, WorkItemFormDto dto, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(dto.Title)) return Result.Failure("Informe o título.");
        var t = await tasks.GetByIdAsync(id, ct);
        if (t is null) return Result.Failure("Tarefa não encontrada.");
        Apply(dto, t);
        tasks.Update(t);
        await uow.SaveChangesAsync(ct);
        return Result.Success();
    }

    public async Task<Result> ToggleDoneAsync(Guid id, CancellationToken ct = default)
    {
        var t = await tasks.GetByIdAsync(id, ct);
        if (t is null) return Result.Failure("Tarefa não encontrada.");
        if (t.Status == WorkItemStatus.Done)
        {
            t.Status = WorkItemStatus.Todo;
            t.CompletedAt = null;
        }
        else
        {
            t.Status = WorkItemStatus.Done;
            t.CompletedAt = DateTime.UtcNow;
        }
        tasks.Update(t);
        await uow.SaveChangesAsync(ct);
        return Result.Success();
    }

    public async Task<Result> ChangeStatusAsync(Guid id, WorkItemStatus newStatus, CancellationToken ct = default)
    {
        var t = await tasks.GetByIdAsync(id, ct);
        if (t is null) return Result.Failure("Tarefa não encontrada.");
        t.Status = newStatus;
        if (newStatus == WorkItemStatus.Done) t.CompletedAt ??= DateTime.UtcNow;
        else if (t.Status != WorkItemStatus.Done) t.CompletedAt = null;
        tasks.Update(t);
        await uow.SaveChangesAsync(ct);
        return Result.Success();
    }

    public async Task<Result> DeleteAsync(Guid id, CancellationToken ct = default)
    {
        var t = await tasks.GetByIdAsync(id, ct);
        if (t is null) return Result.Failure("Tarefa não encontrada.");
        tasks.Remove(t);
        await uow.SaveChangesAsync(ct);
        return Result.Success();
    }

    private static void Apply(WorkItemFormDto dto, WorkItem t)
    {
        t.Title = dto.Title.Trim();
        t.Description = dto.Description?.Trim();
        t.Priority = dto.Priority;
        var newStatus = dto.Status;
        if (newStatus == WorkItemStatus.Done && t.Status != WorkItemStatus.Done)
            t.CompletedAt = DateTime.UtcNow;
        else if (newStatus != WorkItemStatus.Done)
            t.CompletedAt = null;
        t.Status = newStatus;
        t.Category = dto.Category?.Trim();
        t.Tags = dto.Tags?.Trim();
        t.DueDate = dto.DueDate.HasValue
            ? DateTime.SpecifyKind(dto.DueDate.Value.Date, DateTimeKind.Utc)
            : null;
    }
}
