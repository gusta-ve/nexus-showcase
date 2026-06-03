using Microsoft.EntityFrameworkCore;
using Nexus.Application.Common.Interfaces;
using Nexus.Application.Common.Models;
using Nexus.Application.Features.Notifications;
using Nexus.Domain.Entities.Tickets;
using Nexus.Domain.Enums;

namespace Nexus.Application.Features.Tickets;

public class TicketService(
    IRepository<Ticket> tickets,
    IRepository<TicketComment> comments,
    IUnitOfWork uow,
    IAlertService alerts,
    IPortalAccessService portalAccess)
{
    public async Task<PagedResult<TicketListItemDto>> GetPagedAsync(
        string? search,
        IReadOnlyList<TicketStatus>? statuses,
        IReadOnlyList<TicketPriority>? priorities,
        int page, int pageSize,
        CancellationToken ct = default)
    {
        page = page < 1 ? 1 : page;
        pageSize = pageSize is < 1 or > 100 ? 20 : pageSize;

        var query = tickets.Query();

        if (statuses is { Count: > 0 })
            query = query.Where(t => statuses.Contains(t.Status));

        if (priorities is { Count: > 0 })
            query = query.Where(t => priorities.Contains(t.Priority));

        if (!string.IsNullOrWhiteSpace(search))
        {
            var s = search.Trim().ToLower();
            query = query.Where(t =>
                t.Number.ToLower().Contains(s) ||
                t.Title.ToLower().Contains(s) ||
                (t.ContactName != null && t.ContactName.ToLower().Contains(s)) ||
                (t.Client != null && t.Client.Name.ToLower().Contains(s)));
        }

        var total = await query.CountAsync(ct);

        var items = await query
            .OrderByDescending(t => t.Status == TicketStatus.Closed || t.Status == TicketStatus.Cancelled ? 0 : 1)
            .ThenByDescending(t => t.Priority)
            .ThenByDescending(t => t.CreatedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(t => new TicketListItemDto
            {
                Id = t.Id,
                Number = t.Number,
                Title = t.Title,
                ClientId = t.ClientId,
                ClientName = t.Client != null ? t.Client.Name : null,
                ContactName = t.ContactName,
                Status = t.Status,
                Priority = t.Priority,
                Category = t.Category,
                IsFromPublic = t.IsFromPublic,
                CreatedAt = t.CreatedAt,
                ResolvedAt = t.ResolvedAt
            })
            .ToListAsync(ct);

        return new PagedResult<TicketListItemDto>
        {
            Items = items, TotalCount = total, Page = page, PageSize = pageSize
        };
    }

    public async Task<TicketDetailDto?> GetDetailAsync(Guid id, CancellationToken ct = default)
    {
        return await tickets.Query()
            .Where(t => t.Id == id)
            .Select(t => new TicketDetailDto
            {
                Id = t.Id,
                Number = t.Number,
                Title = t.Title,
                Description = t.Description,
                ClientId = t.ClientId,
                ClientName = t.Client != null ? t.Client.Name : null,
                ContactName = t.ContactName,
                ContactEmail = t.ContactEmail,
                ContactPhone = t.ContactPhone,
                ContactWhatsApp = t.ContactWhatsApp,
                PreferredTime = t.PreferredTime,
                Status = t.Status,
                Priority = t.Priority,
                Category = t.Category,
                CreatedAt = t.CreatedAt,
                StartedAt = t.StartedAt,
                ResolvedAt = t.ResolvedAt,
                ClosedAt = t.ClosedAt,
                SlaDeadline = t.SlaDeadline,
                Resolution = t.Resolution,
                InternalNotes = t.InternalNotes,
                ServiceValue = t.ServiceValue,
                IsFromPublic = t.IsFromPublic,
                Comments = t.Comments
                    .OrderBy(c => c.CreatedAt)
                    .Select(c => new TicketCommentDto
                    {
                        Id = c.Id,
                        Content = c.Content,
                        IsInternal = c.IsInternal,
                        IsFromClient = c.IsFromClient,
                        AuthorName = c.AuthorName,
                        CreatedAt = c.CreatedAt
                    }).ToList()
            })
            .FirstOrDefaultAsync(ct);
    }

    public async Task<Result<Guid>> CreateAsync(TicketFormDto dto, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(dto.Title))
            return Result<Guid>.Failure("Título é obrigatório.");

        var ticket = new Ticket
        {
            Number = await GenerateNumberAsync(ct),
            Status = TicketStatus.Open,
            IsFromPublic = false
        };
        Apply(dto, ticket);

        await tickets.AddAsync(ticket, ct);
        await uow.SaveChangesAsync(ct);
        return Result<Guid>.Success(ticket.Id);
    }

    public async Task<Result> UpdateAsync(Guid id, TicketFormDto dto, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(dto.Title))
            return Result.Failure("Título é obrigatório.");

        var ticket = await tickets.GetByIdAsync(id, ct);
        if (ticket is null) return Result.Failure("Chamado não encontrado.");

        Apply(dto, ticket);
        tickets.Update(ticket);
        await uow.SaveChangesAsync(ct);
        return Result.Success();
    }

    public async Task<Result> ChangeStatusAsync(Guid id, TicketStatus newStatus, CancellationToken ct = default)
    {
        var ticket = await tickets.GetByIdAsync(id, ct);
        if (ticket is null) return Result.Failure("Chamado não encontrado.");

        var now = DateTime.UtcNow;
        ticket.Status = newStatus;

        switch (newStatus)
        {
            case TicketStatus.InProgress:
                ticket.StartedAt ??= now;
                break;
            case TicketStatus.Resolved:
                ticket.ResolvedAt ??= now;
                break;
            case TicketStatus.Closed:
                ticket.ClosedAt ??= now;
                if (ticket.ResolvedAt is null) ticket.ResolvedAt = now;
                break;
            case TicketStatus.Open:
                // Reopening clears closure timestamps
                ticket.ClosedAt = null;
                if (newStatus == TicketStatus.Open) ticket.ResolvedAt = null;
                break;
        }

        tickets.Update(ticket);
        await uow.SaveChangesAsync(ct);

        // notifica o cliente quando o status muda (e ele tem portal)
        await NotifyClientOfStatusChangeAsync(ticket, ct);
        return Result.Success();
    }

    public async Task<Result> ResolveAsync(Guid id, ResolveTicketDto dto, CancellationToken ct = default)
    {
        var ticket = await tickets.GetByIdAsync(id, ct);
        if (ticket is null) return Result.Failure("Chamado não encontrado.");

        ticket.Status = TicketStatus.Resolved;
        ticket.ResolvedAt = DateTime.UtcNow;
        if (!string.IsNullOrWhiteSpace(dto.Resolution))
            ticket.Resolution = dto.Resolution.Trim();
        if (dto.ServiceValue.HasValue)
            ticket.ServiceValue = dto.ServiceValue;

        tickets.Update(ticket);
        await uow.SaveChangesAsync(ct);

        await NotifyClientOfStatusChangeAsync(ticket, ct);
        return Result.Success();
    }

    public async Task<Result> AddCommentAsync(Guid ticketId, AddCommentDto dto, string? authorName, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(dto.Content))
            return Result.Failure("Conteúdo é obrigatório.");

        var ticket = await tickets.GetByIdAsync(ticketId, ct);
        if (ticket is null) return Result.Failure("Chamado não encontrado.");

        var comment = new TicketComment
        {
            TicketId = ticketId,
            Content = dto.Content.Trim(),
            IsInternal = dto.IsInternal,
            IsFromClient = false,
            AuthorName = authorName ?? "Admin"
        };
        await comments.AddAsync(comment, ct);
        await uow.SaveChangesAsync(ct);

        // comentário interno NÃO vai pro cliente
        if (!dto.IsInternal && ticket.ClientId is { } cid)
        {
            var uid = await portalAccess.GetUserIdForClientAsync(cid, ct);
            if (uid is not null)
            {
                await alerts.CreateAndDispatchAsync(
                    type: AlertType.SystemEvent,
                    severity: AlertSeverity.Info,
                    title: "Nova resposta no seu chamado",
                    message: $"{authorName ?? "Atendimento"} respondeu em {ticket.Number}",
                    targetUserId: uid,
                    actionUrl: $"/portal/chamados/{ticketId}",
                    relatedEntityId: ticketId,
                    relatedEntityType: nameof(Ticket),
                    ct: ct);
            }
        }
        return Result.Success();
    }

    private async Task NotifyClientOfStatusChangeAsync(Ticket ticket, CancellationToken ct)
    {
        if (ticket.ClientId is not { } cid) return;
        var uid = await portalAccess.GetUserIdForClientAsync(cid, ct);
        if (uid is null) return;

        var (title, severity) = ticket.Status switch
        {
            TicketStatus.InProgress  => ("Chamado em atendimento", AlertSeverity.Info),
            TicketStatus.WaitingClient => ("Aguardando sua resposta", AlertSeverity.Warning),
            TicketStatus.Resolved    => ("Chamado resolvido", AlertSeverity.Info),
            TicketStatus.Closed      => ("Chamado encerrado", AlertSeverity.Info),
            TicketStatus.Cancelled   => ("Chamado cancelado", AlertSeverity.Warning),
            _ => (string.Empty, AlertSeverity.Info)
        };
        if (string.IsNullOrEmpty(title)) return;

        await alerts.CreateAndDispatchAsync(
            type: AlertType.SystemEvent,
            severity: severity,
            title: title,
            message: $"{ticket.Number} — {ticket.Title}",
            targetUserId: uid,
            actionUrl: $"/portal/chamados/{ticket.Id}",
            relatedEntityId: ticket.Id,
            relatedEntityType: nameof(Ticket),
            ct: ct);
    }

    public async Task<Result> LinkClientAsync(Guid ticketId, Guid clientId, CancellationToken ct = default)
    {
        var ticket = await tickets.GetByIdAsync(ticketId, ct);
        if (ticket is null) return Result.Failure("Chamado não encontrado.");

        ticket.ClientId = clientId;
        tickets.Update(ticket);
        await uow.SaveChangesAsync(ct);
        return Result.Success();
    }

    public async Task<Result> DeleteAsync(Guid id, CancellationToken ct = default)
    {
        var ticket = await tickets.GetByIdAsync(id, ct);
        if (ticket is null) return Result.Failure("Chamado não encontrado.");

        tickets.Remove(ticket);
        await uow.SaveChangesAsync(ct);
        return Result.Success();
    }

    private async Task<string> GenerateNumberAsync(CancellationToken ct)
    {
        var today = DateTime.UtcNow;
        var prefix = $"NX-{today:yyMMdd}-";
        var countToday = await tickets.Query().CountAsync(t => t.Number.StartsWith(prefix), ct);
        return $"{prefix}{(countToday + 1):D4}";
    }

    private static void Apply(TicketFormDto dto, Ticket t)
    {
        t.Title = dto.Title.Trim();
        t.Description = dto.Description?.Trim();
        t.ClientId = dto.ClientId;
        t.ContactName = dto.ContactName?.Trim();
        t.ContactEmail = dto.ContactEmail?.Trim();
        t.ContactPhone = dto.ContactPhone?.Trim();
        t.ContactWhatsApp = dto.ContactWhatsApp?.Trim();
        t.PreferredTime = dto.PreferredTime?.Trim();
        t.Priority = dto.Priority;
        t.Category = dto.Category;
        t.InternalNotes = dto.InternalNotes?.Trim();
        t.ServiceValue = dto.ServiceValue;
    }
}
