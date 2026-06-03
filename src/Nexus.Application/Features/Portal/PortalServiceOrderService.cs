using Microsoft.EntityFrameworkCore;
using Nexus.Application.Common.Interfaces;
using Nexus.Application.Common.Models;
using Nexus.Domain.Entities.Documents;
using Nexus.Domain.Enums;

namespace Nexus.Application.Features.Portal;

// DTOs sem TechnicianNotes — bastidor técnico não vai pro cliente.
// Checklist é OK exibir: cliente sabe o que foi feito.

public class PortalServiceOrderListDto
{
    public Guid Id { get; set; }
    public string Number { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public DocumentStatus Status { get; set; }
    public decimal? Total { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? ScheduledDate { get; set; }
    public DateTime? CompletedAt { get; set; }
}

public class PortalServiceOrderDetailDto
{
    public Guid Id { get; set; }
    public string Number { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string? Description { get; set; }
    public DocumentStatus Status { get; set; }
    public DateTime? ScheduledDate { get; set; }
    public DateTime? StartedAt { get; set; }
    public DateTime? CompletedAt { get; set; }
    public string? Checklist { get; set; }
    public decimal? Total { get; set; }
    public DateTime CreatedAt { get; set; }
}

public class PortalServiceOrderService(IRepository<ServiceOrder> orders)
{
    public async Task<PagedResult<PortalServiceOrderListDto>> GetMyOrdersAsync(
        Guid clientId, int page, int pageSize, CancellationToken ct = default)
    {
        page = page < 1 ? 1 : page;
        pageSize = pageSize is < 1 or > 100 ? 20 : pageSize;

        // só OS que saíram do rascunho — rascunho é coisa de admin
        var q = orders.Query().Where(x => x.ClientId == clientId && x.Status != DocumentStatus.Draft);

        var total = await q.CountAsync(ct);
        var list = await q
            .OrderByDescending(x => x.ScheduledDate ?? x.CreatedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(x => new PortalServiceOrderListDto
            {
                Id = x.Id,
                Number = x.Number,
                Title = x.Title,
                Status = x.Status,
                Total = x.Total,
                CreatedAt = x.CreatedAt,
                ScheduledDate = x.ScheduledDate,
                CompletedAt = x.CompletedAt
            })
            .ToListAsync(ct);

        return new PagedResult<PortalServiceOrderListDto> { Items = list, TotalCount = total, Page = page, PageSize = pageSize };
    }

    public async Task<PortalServiceOrderDetailDto?> GetDetailAsync(
        Guid id, Guid clientId, CancellationToken ct = default)
    {
        return await orders.Query()
            .Where(x => x.Id == id && x.ClientId == clientId && x.Status != DocumentStatus.Draft)
            .Select(x => new PortalServiceOrderDetailDto
            {
                Id = x.Id,
                Number = x.Number,
                Title = x.Title,
                Description = x.Description,
                Status = x.Status,
                ScheduledDate = x.ScheduledDate,
                StartedAt = x.StartedAt,
                CompletedAt = x.CompletedAt,
                Checklist = x.Checklist,
                Total = x.Total,
                CreatedAt = x.CreatedAt
            })
            .FirstOrDefaultAsync(ct);
    }
}
