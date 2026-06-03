using Microsoft.EntityFrameworkCore;
using Nexus.Application.Common.Interfaces;
using Nexus.Application.Common.Models;
using Nexus.Application.Features.Notifications;
using Nexus.Domain.Entities.Documents;
using Nexus.Domain.Enums;

namespace Nexus.Application.Features.Portal;

// DTOs enxutos pro portal: cliente NÃO vê Notes internas, Terms admin, Discount/Tax brutos
// nem qualquer campo que exponha lógica de bastidor.

public class PortalQuoteListDto
{
    public Guid Id { get; set; }
    public string Number { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public DocumentStatus Status { get; set; }
    public decimal Total { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? ValidUntil { get; set; }
    public DateTime? SentAt { get; set; }
}

public class PortalQuoteDetailDto
{
    public Guid Id { get; set; }
    public string Number { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string? Description { get; set; }
    public DocumentStatus Status { get; set; }
    public DateTime? ValidUntil { get; set; }
    public DateTime? SentAt { get; set; }
    public DateTime? AcceptedAt { get; set; }
    public decimal Total { get; set; }
    public DateTime CreatedAt { get; set; }
    public List<PortalQuoteItemDto> Items { get; set; } = [];
    // se o admin quiser deixar termos/observações visíveis pro cliente, podem ir aqui
    // (Notes/Terms da Quote já são pensados pra serem mostrados — InternalNotes seria coisa de admin que NÃO existe aqui)
    public string? Notes { get; set; }
    public string? Terms { get; set; }
}

public class PortalQuoteItemDto
{
    public string Description { get; set; } = string.Empty;
    public decimal UnitPrice { get; set; }
    public decimal Quantity { get; set; }
    public decimal Total { get; set; }
}

public class PortalQuoteService(IRepository<Quote> quotes, IUnitOfWork uow, IAlertService alerts)
{
    public async Task<PagedResult<PortalQuoteListDto>> GetMyQuotesAsync(
        Guid clientId, int page, int pageSize, CancellationToken ct = default)
    {
        page = page < 1 ? 1 : page;
        pageSize = pageSize is < 1 or > 100 ? 20 : pageSize;

        // só lista os que JÁ foram enviados — rascunho é coisa do admin
        var q = quotes.Query().Where(x => x.ClientId == clientId && x.Status != DocumentStatus.Draft);

        var total = await q.CountAsync(ct);
        var list = await q
            .OrderByDescending(x => x.SentAt ?? x.CreatedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(x => new PortalQuoteListDto
            {
                Id = x.Id,
                Number = x.Number,
                Title = x.Title,
                Status = x.Status,
                Total = x.Total,
                CreatedAt = x.CreatedAt,
                ValidUntil = x.ValidUntil,
                SentAt = x.SentAt
            })
            .ToListAsync(ct);

        return new PagedResult<PortalQuoteListDto> { Items = list, TotalCount = total, Page = page, PageSize = pageSize };
    }

    public async Task<PortalQuoteDetailDto?> GetDetailAsync(
        Guid id, Guid clientId, CancellationToken ct = default)
    {
        // filtro duplo: id + clientId — cliente nunca acessa orçamento que não é dele
        return await quotes.Query()
            .Where(x => x.Id == id && x.ClientId == clientId && x.Status != DocumentStatus.Draft)
            .Select(x => new PortalQuoteDetailDto
            {
                Id = x.Id,
                Number = x.Number,
                Title = x.Title,
                Description = x.Description,
                Status = x.Status,
                ValidUntil = x.ValidUntil,
                SentAt = x.SentAt,
                AcceptedAt = x.AcceptedAt,
                Total = x.Total,
                CreatedAt = x.CreatedAt,
                Notes = x.Notes,
                Terms = x.Terms,
                Items = x.Items
                    .OrderBy(i => i.DisplayOrder)
                    .Select(i => new PortalQuoteItemDto
                    {
                        Description = i.Description,
                        UnitPrice = i.UnitPrice,
                        Quantity = i.Quantity,
                        Total = i.Total
                    }).ToList()
            })
            .FirstOrDefaultAsync(ct);
    }

    public async Task<Result> AcceptAsync(Guid id, Guid clientId, CancellationToken ct = default)
    {
        var q = await quotes.Query().FirstOrDefaultAsync(x => x.Id == id && x.ClientId == clientId, ct);
        if (q is null) return Result.Failure("Orçamento não encontrado.");
        if (q.Status != DocumentStatus.Sent)
            return Result.Failure("Esse orçamento não está disponível pra aprovação.");
        if (q.ValidUntil is not null && q.ValidUntil < DateTime.UtcNow.Date)
            return Result.Failure("Orçamento expirado. Solicite uma nova proposta.");

        q.Status = DocumentStatus.Accepted;
        q.AcceptedAt = DateTime.UtcNow;
        quotes.Update(q);
        await uow.SaveChangesAsync(ct);

        await alerts.CreateAndDispatchAsync(
            type: AlertType.SystemEvent,
            severity: AlertSeverity.Info,
            title: "Orçamento aprovado",
            message: $"Cliente aprovou {q.Number} — pode emitir a OS",
            actionUrl: $"/orcamentos/{q.Id}",
            relatedEntityId: q.Id,
            relatedEntityType: nameof(Quote),
            ct: ct);

        return Result.Success();
    }

    public async Task<Result> RejectAsync(Guid id, Guid clientId, CancellationToken ct = default)
    {
        var q = await quotes.Query().FirstOrDefaultAsync(x => x.Id == id && x.ClientId == clientId, ct);
        if (q is null) return Result.Failure("Orçamento não encontrado.");
        if (q.Status != DocumentStatus.Sent)
            return Result.Failure("Esse orçamento não está disponível pra resposta.");

        q.Status = DocumentStatus.Rejected;
        quotes.Update(q);
        await uow.SaveChangesAsync(ct);

        await alerts.CreateAndDispatchAsync(
            type: AlertType.SystemEvent,
            severity: AlertSeverity.Warning,
            title: "Orçamento recusado",
            message: $"Cliente recusou {q.Number}",
            actionUrl: $"/orcamentos/{q.Id}",
            relatedEntityId: q.Id,
            relatedEntityType: nameof(Quote),
            ct: ct);

        return Result.Success();
    }
}
