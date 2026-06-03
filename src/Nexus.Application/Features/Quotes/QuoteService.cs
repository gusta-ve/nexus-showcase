using Microsoft.EntityFrameworkCore;
using Nexus.Application.Common.Interfaces;
using Nexus.Application.Common.Models;
using Nexus.Application.Features.Notifications;
using Nexus.Domain.Entities.Documents;
using Nexus.Domain.Enums;

namespace Nexus.Application.Features.Quotes;

public class QuoteService(
    IRepository<Quote> quotes,
    IRepository<QuoteItem> items,
    IUnitOfWork uow,
    IAlertService alerts,
    IPortalAccessService portalAccess)
{
    public async Task<PagedResult<QuoteListItemDto>> GetPagedAsync(
        string? search, IReadOnlyList<DocumentStatus>? statuses,
        int page, int pageSize, CancellationToken ct = default)
    {
        page = page < 1 ? 1 : page;
        pageSize = pageSize is < 1 or > 100 ? 20 : pageSize;

        var q = quotes.Query();
        if (statuses is { Count: > 0 }) q = q.Where(x => statuses.Contains(x.Status));
        if (!string.IsNullOrWhiteSpace(search))
        {
            var s = search.Trim().ToLower();
            q = q.Where(x => x.Number.ToLower().Contains(s)
                          || x.Title.ToLower().Contains(s)
                          || (x.Client != null && x.Client.Name.ToLower().Contains(s))
                          || (x.ClientName != null && x.ClientName.ToLower().Contains(s)));
        }

        var total = await q.CountAsync(ct);
        var list = await q
            .OrderByDescending(x => x.CreatedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(x => new QuoteListItemDto
            {
                Id = x.Id,
                Number = x.Number,
                Title = x.Title,
                ClientName = x.Client != null ? x.Client.Name : x.ClientName,
                Status = x.Status,
                Total = x.Total,
                CreatedAt = x.CreatedAt,
                ValidUntil = x.ValidUntil,
                SentAt = x.SentAt
            })
            .ToListAsync(ct);

        return new PagedResult<QuoteListItemDto> { Items = list, TotalCount = total, Page = page, PageSize = pageSize };
    }

    public async Task<QuoteDetailDto?> GetDetailAsync(Guid id, CancellationToken ct = default)
    {
        return await quotes.Query()
            .Where(x => x.Id == id)
            .Select(x => new QuoteDetailDto
            {
                Id = x.Id,
                Number = x.Number,
                Title = x.Title,
                Description = x.Description,
                ClientId = x.ClientId,
                ClientName = x.Client != null ? x.Client.Name : x.ClientName,
                ClientEmail = x.Client != null ? x.Client.Email : null,
                ClientWhatsApp = x.Client != null ? x.Client.WhatsApp : null,
                Status = x.Status,
                ValidUntil = x.ValidUntil,
                SentAt = x.SentAt,
                AcceptedAt = x.AcceptedAt,
                Subtotal = x.Subtotal,
                Discount = x.Discount,
                Tax = x.Tax,
                Total = x.Total,
                Notes = x.Notes,
                Terms = x.Terms,
                CreatedAt = x.CreatedAt,
                Items = x.Items
                    .OrderBy(i => i.DisplayOrder)
                    .Select(i => new QuoteItemDto
                    {
                        Id = i.Id,
                        Description = i.Description,
                        UnitPrice = i.UnitPrice,
                        Quantity = i.Quantity,
                        Total = i.Total,
                        DisplayOrder = i.DisplayOrder
                    }).ToList()
            })
            .FirstOrDefaultAsync(ct);
    }

    public async Task<QuoteFormDto?> GetForEditAsync(Guid id, CancellationToken ct = default)
    {
        var q = await quotes.GetByIdAsync(id, ct);
        if (q is null) return null;
        return new QuoteFormDto
        {
            Id = q.Id,
            Title = q.Title,
            Description = q.Description,
            ClientId = q.ClientId,
            ClientNameOverride = q.ClientId is null ? q.ClientName : null,
            ValidUntil = q.ValidUntil,
            Discount = q.Discount,
            Tax = q.Tax,
            Notes = q.Notes,
            Terms = q.Terms
        };
    }

    public async Task<Result<Guid>> CreateAsync(QuoteFormDto dto, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(dto.Title)) return Result<Guid>.Failure("Informe o título.");

        var q = new Quote
        {
            Number = await GenerateNumberAsync(ct),
            Status = DocumentStatus.Draft
        };
        Apply(dto, q);
        await quotes.AddAsync(q, ct);
        await uow.SaveChangesAsync(ct);
        return Result<Guid>.Success(q.Id);
    }

    public async Task<Result> UpdateAsync(Guid id, QuoteFormDto dto, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(dto.Title)) return Result.Failure("Informe o título.");
        var q = await quotes.GetByIdAsync(id, ct);
        if (q is null) return Result.Failure("Orçamento não encontrado.");
        Apply(dto, q);
        await RecalculateAsync(q.Id, ct);  // garante totais
        return Result.Success();
    }

    public async Task<Result<Guid>> AddItemAsync(Guid quoteId, QuoteItemFormDto dto, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(dto.Description)) return Result<Guid>.Failure("Descreva o item.");
        var q = await quotes.GetByIdAsync(quoteId, ct);
        if (q is null) return Result<Guid>.Failure("Orçamento não encontrado.");
        if (q.Status != DocumentStatus.Draft)
            return Result<Guid>.Failure("Só é possível editar itens enquanto o orçamento está em rascunho.");

        var nextOrder = await items.Query().Where(i => i.QuoteId == quoteId).MaxAsync(i => (int?)i.DisplayOrder, ct) ?? 0;

        var item = new QuoteItem
        {
            QuoteId = quoteId,
            Description = dto.Description.Trim(),
            UnitPrice = dto.UnitPrice,
            Quantity = dto.Quantity,
            Total = Math.Round(dto.UnitPrice * dto.Quantity, 2),
            DisplayOrder = nextOrder + 1
        };
        await items.AddAsync(item, ct);
        await uow.SaveChangesAsync(ct);
        await RecalculateAsync(quoteId, ct);
        return Result<Guid>.Success(item.Id);
    }

    public async Task<Result> UpdateItemAsync(Guid itemId, QuoteItemFormDto dto, CancellationToken ct = default)
    {
        var i = await items.GetByIdAsync(itemId, ct);
        if (i is null) return Result.Failure("Item não encontrado.");
        var q = await quotes.GetByIdAsync(i.QuoteId, ct);
        if (q is null) return Result.Failure("Orçamento não encontrado.");
        if (q.Status != DocumentStatus.Draft) return Result.Failure("Orçamento não está em rascunho.");

        i.Description = dto.Description.Trim();
        i.UnitPrice = dto.UnitPrice;
        i.Quantity = dto.Quantity;
        i.Total = Math.Round(dto.UnitPrice * dto.Quantity, 2);
        items.Update(i);
        await uow.SaveChangesAsync(ct);
        await RecalculateAsync(i.QuoteId, ct);
        return Result.Success();
    }

    public async Task<Result> RemoveItemAsync(Guid itemId, CancellationToken ct = default)
    {
        var i = await items.GetByIdAsync(itemId, ct);
        if (i is null) return Result.Failure("Item não encontrado.");
        var q = await quotes.GetByIdAsync(i.QuoteId, ct);
        if (q is null) return Result.Failure("Orçamento não encontrado.");
        if (q.Status != DocumentStatus.Draft) return Result.Failure("Orçamento não está em rascunho.");

        items.Remove(i);
        await uow.SaveChangesAsync(ct);
        await RecalculateAsync(i.QuoteId, ct);
        return Result.Success();
    }

    public async Task<Result> ChangeStatusAsync(Guid id, DocumentStatus newStatus, CancellationToken ct = default)
    {
        var q = await quotes.GetByIdAsync(id, ct);
        if (q is null) return Result.Failure("Orçamento não encontrado.");

        var now = DateTime.UtcNow;
        q.Status = newStatus;
        switch (newStatus)
        {
            case DocumentStatus.Sent:
                q.SentAt ??= now;
                break;
            case DocumentStatus.Accepted:
                q.AcceptedAt ??= now;
                break;
        }
        quotes.Update(q);
        await uow.SaveChangesAsync(ct);

        // quando o admin marca como Sent, o cliente recebe push
        if (newStatus == DocumentStatus.Sent && q.ClientId is { } cid)
        {
            var uid = await portalAccess.GetUserIdForClientAsync(cid, ct);
            if (uid is not null)
            {
                await alerts.CreateAndDispatchAsync(
                    type: AlertType.SystemEvent,
                    severity: AlertSeverity.Info,
                    title: "Novo orçamento pra você",
                    message: $"{q.Number} — {q.Title}",
                    targetUserId: uid,
                    actionUrl: $"/portal/orcamentos/{q.Id}",
                    relatedEntityId: q.Id,
                    relatedEntityType: nameof(Quote),
                    ct: ct);
            }
        }
        return Result.Success();
    }

    public async Task<Result> DeleteAsync(Guid id, CancellationToken ct = default)
    {
        var q = await quotes.GetByIdAsync(id, ct);
        if (q is null) return Result.Failure("Orçamento não encontrado.");
        quotes.Remove(q);
        await uow.SaveChangesAsync(ct);
        return Result.Success();
    }

    public async Task RecalculateAsync(Guid quoteId, CancellationToken ct = default)
    {
        var q = await quotes.Query()
            .Include(x => x.Items)
            .FirstOrDefaultAsync(x => x.Id == quoteId, ct);
        if (q is null) return;

        q.Subtotal = Math.Round(q.Items.Sum(i => i.Total), 2);
        q.Total = Math.Max(0, Math.Round(q.Subtotal - q.Discount + q.Tax, 2));
        quotes.Update(q);
        await uow.SaveChangesAsync(ct);
    }

    private async Task<string> GenerateNumberAsync(CancellationToken ct)
    {
        var today = DateTime.UtcNow;
        var prefix = $"ORC-{today:yyMMdd}-";
        var countToday = await quotes.Query().CountAsync(q => q.Number.StartsWith(prefix), ct);
        return $"{prefix}{(countToday + 1):D3}";
    }

    private static void Apply(QuoteFormDto dto, Quote q)
    {
        q.Title = dto.Title.Trim();
        q.Description = dto.Description?.Trim();
        q.ClientId = dto.ClientId;
        q.ClientName = dto.ClientId is null ? dto.ClientNameOverride?.Trim() : null;
        q.ValidUntil = dto.ValidUntil.HasValue
            ? DateTime.SpecifyKind(dto.ValidUntil.Value.Date, DateTimeKind.Utc) : null;
        q.Discount = dto.Discount;
        q.Tax = dto.Tax;
        q.Notes = dto.Notes?.Trim();
        q.Terms = dto.Terms?.Trim();
    }
}
