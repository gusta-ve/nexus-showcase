using Microsoft.EntityFrameworkCore;
using Nexus.Application.Common.Interfaces;
using Nexus.Application.Common.Models;
using Nexus.Domain.Entities.Documents;
using Nexus.Domain.Enums;

namespace Nexus.Application.Features.ServiceOrders;

public class ServiceOrderService(IRepository<ServiceOrder> orders, IUnitOfWork uow)
{
    public async Task<PagedResult<ServiceOrderListItemDto>> GetPagedAsync(
        string? search, IReadOnlyList<DocumentStatus>? statuses,
        int page, int pageSize, CancellationToken ct = default)
    {
        page = page < 1 ? 1 : page;
        pageSize = pageSize is < 1 or > 100 ? 20 : pageSize;

        var q = orders.Query();
        if (statuses is { Count: > 0 }) q = q.Where(x => statuses.Contains(x.Status));
        if (!string.IsNullOrWhiteSpace(search))
        {
            var s = search.Trim().ToLower();
            q = q.Where(x => x.Number.ToLower().Contains(s)
                          || x.Title.ToLower().Contains(s)
                          || (x.Client != null && x.Client.Name.ToLower().Contains(s)));
        }

        var total = await q.CountAsync(ct);
        var list = await q
            .OrderByDescending(x => x.CreatedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(x => new ServiceOrderListItemDto
            {
                Id = x.Id,
                Number = x.Number,
                Title = x.Title,
                ClientName = x.Client != null ? x.Client.Name : null,
                Status = x.Status,
                Total = x.Total,
                CreatedAt = x.CreatedAt,
                ScheduledDate = x.ScheduledDate,
                CompletedAt = x.CompletedAt
            })
            .ToListAsync(ct);

        return new PagedResult<ServiceOrderListItemDto> { Items = list, TotalCount = total, Page = page, PageSize = pageSize };
    }

    public async Task<ServiceOrderDetailDto?> GetDetailAsync(Guid id, CancellationToken ct = default)
    {
        return await orders.Query()
            .Where(x => x.Id == id)
            .Select(x => new ServiceOrderDetailDto
            {
                Id = x.Id,
                Number = x.Number,
                Title = x.Title,
                Description = x.Description,
                ClientId = x.ClientId,
                ClientName = x.Client != null ? x.Client.Name : null,
                TicketId = x.TicketId,
                TicketNumber = x.Ticket != null ? x.Ticket.Number : null,
                Status = x.Status,
                ScheduledDate = x.ScheduledDate,
                StartedAt = x.StartedAt,
                CompletedAt = x.CompletedAt,
                TechnicianNotes = x.TechnicianNotes,
                Checklist = x.Checklist,
                LaborValue = x.LaborValue,
                PartsValue = x.PartsValue,
                Discount = x.Discount,
                Total = x.Total,
                CreatedAt = x.CreatedAt
            })
            .FirstOrDefaultAsync(ct);
    }

    public async Task<ServiceOrderFormDto?> GetForEditAsync(Guid id, CancellationToken ct = default)
    {
        var o = await orders.GetByIdAsync(id, ct);
        if (o is null) return null;
        return new ServiceOrderFormDto
        {
            Id = o.Id,
            Title = o.Title,
            Description = o.Description,
            ClientId = o.ClientId,
            TicketId = o.TicketId,
            ScheduledDate = o.ScheduledDate,
            TechnicianNotes = o.TechnicianNotes,
            Checklist = o.Checklist,
            LaborValue = o.LaborValue,
            PartsValue = o.PartsValue,
            Discount = o.Discount
        };
    }

    public async Task<Result<Guid>> CreateAsync(ServiceOrderFormDto dto, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(dto.Title)) return Result<Guid>.Failure("Informe o título.");
        var o = new ServiceOrder
        {
            Number = await GenerateNumberAsync(ct),
            Status = DocumentStatus.Draft
        };
        Apply(dto, o);
        await orders.AddAsync(o, ct);
        await uow.SaveChangesAsync(ct);
        return Result<Guid>.Success(o.Id);
    }

    public async Task<Result> UpdateAsync(Guid id, ServiceOrderFormDto dto, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(dto.Title)) return Result.Failure("Informe o título.");
        var o = await orders.GetByIdAsync(id, ct);
        if (o is null) return Result.Failure("OS não encontrada.");
        Apply(dto, o);
        orders.Update(o);
        await uow.SaveChangesAsync(ct);
        return Result.Success();
    }

    public async Task<Result> ChangeStatusAsync(Guid id, DocumentStatus newStatus, CancellationToken ct = default)
    {
        var o = await orders.GetByIdAsync(id, ct);
        if (o is null) return Result.Failure("OS não encontrada.");

        var now = DateTime.UtcNow;
        o.Status = newStatus;
        switch (newStatus)
        {
            case DocumentStatus.Sent:
                o.StartedAt ??= now;
                break;
            case DocumentStatus.Accepted:
                o.CompletedAt ??= now;
                break;
        }
        orders.Update(o);
        await uow.SaveChangesAsync(ct);
        return Result.Success();
    }

    public async Task<Result> DeleteAsync(Guid id, CancellationToken ct = default)
    {
        var o = await orders.GetByIdAsync(id, ct);
        if (o is null) return Result.Failure("OS não encontrada.");
        orders.Remove(o);
        await uow.SaveChangesAsync(ct);
        return Result.Success();
    }

    private async Task<string> GenerateNumberAsync(CancellationToken ct)
    {
        var today = DateTime.UtcNow;
        var prefix = $"OS-{today:yyMMdd}-";
        var countToday = await orders.Query().CountAsync(o => o.Number.StartsWith(prefix), ct);
        return $"{prefix}{(countToday + 1):D3}";
    }

    private static void Apply(ServiceOrderFormDto dto, ServiceOrder o)
    {
        o.Title = dto.Title.Trim();
        o.Description = dto.Description?.Trim();
        o.ClientId = dto.ClientId;
        o.TicketId = dto.TicketId;
        o.ScheduledDate = dto.ScheduledDate.HasValue
            ? DateTime.SpecifyKind(dto.ScheduledDate.Value, DateTimeKind.Utc) : null;
        o.TechnicianNotes = dto.TechnicianNotes?.Trim();
        o.Checklist = dto.Checklist?.Trim();
        o.LaborValue = dto.LaborValue;
        o.PartsValue = dto.PartsValue;
        o.Discount = dto.Discount;
        o.Total = Math.Max(0, (dto.LaborValue ?? 0) + (dto.PartsValue ?? 0) - (dto.Discount ?? 0));
    }
}
