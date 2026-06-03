using Microsoft.EntityFrameworkCore;
using Nexus.Application.Common.Interfaces;
using Nexus.Application.Common.Models;
using Nexus.Application.Features.Notifications;
using Nexus.Domain.Entities.Clients;
using Nexus.Domain.Entities.Tickets;
using Nexus.Domain.Enums;

namespace Nexus.Application.Features.Portal;

/// <summary>
/// Serviço do PORTAL DO CLIENTE — TODA query é forçadamente filtrada pelo
/// clientId do usuário logado. Não existe overload sem clientId.
/// Não expõe: InternalNotes, ServiceValue, comentários internos.
/// </summary>
public class PortalTicketService(
    IRepository<Ticket> tickets,
    IRepository<TicketComment> comments,
    IRepository<Client> clients,
    IUnitOfWork uow,
    IAlertService alerts)
{
    public async Task<PagedResult<PortalTicketSummaryDto>> GetMyTicketsAsync(
        Guid clientId, int page, int pageSize, CancellationToken ct = default)
    {
        page = page < 1 ? 1 : page;
        pageSize = pageSize is < 1 or > 100 ? 20 : pageSize;

        var query = tickets.Query().Where(t => t.ClientId == clientId);
        var total = await query.CountAsync(ct);

        var items = await query
            .OrderByDescending(t => t.CreatedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(t => new PortalTicketSummaryDto
            {
                Id = t.Id,
                Number = t.Number,
                Title = t.Title,
                Status = t.Status,
                Priority = t.Priority,
                CreatedAt = t.CreatedAt,
                ResolvedAt = t.ResolvedAt
            })
            .ToListAsync(ct);

        return new PagedResult<PortalTicketSummaryDto> { Items = items, TotalCount = total, Page = page, PageSize = pageSize };
    }

    public async Task<PortalTicketDetailDto?> GetMyTicketAsync(
        Guid clientId, Guid ticketId, CancellationToken ct = default)
    {
        return await tickets.Query()
            .Where(t => t.Id == ticketId && t.ClientId == clientId)
            .Select(t => new PortalTicketDetailDto
            {
                Id = t.Id,
                Number = t.Number,
                Title = t.Title,
                Description = t.Description,
                Status = t.Status,
                Priority = t.Priority,
                Category = t.Category,
                CreatedAt = t.CreatedAt,
                StartedAt = t.StartedAt,
                ResolvedAt = t.ResolvedAt,
                Resolution = t.Resolution,
                // Apenas comentários NÃO-internos:
                Comments = t.Comments
                    .Where(c => !c.IsInternal)
                    .OrderBy(c => c.CreatedAt)
                    .Select(c => new PortalTicketCommentDto
                    {
                        Content = c.Content,
                        IsFromClient = c.IsFromClient,
                        AuthorName = c.AuthorName,
                        CreatedAt = c.CreatedAt
                    }).ToList()
            })
            .FirstOrDefaultAsync(ct);
    }

    public async Task<Result<Guid>> OpenAsync(Guid clientId, PortalOpenTicketDto dto, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(dto.Title))
            return Result<Guid>.Failure("Título é obrigatório.");
        if (string.IsNullOrWhiteSpace(dto.Description))
            return Result<Guid>.Failure("Descreva o problema.");

        var ticket = new Ticket
        {
            ClientId = clientId,
            Title = dto.Title.Trim(),
            Description = dto.Description.Trim(),
            Category = dto.Category,
            Priority = dto.Priority,
            Status = TicketStatus.Open,
            IsFromPublic = false,
            Number = await GenerateNumberAsync(ct),
            TrackingToken = Guid.NewGuid().ToString("N")
        };

        await tickets.AddAsync(ticket, ct);
        await uow.SaveChangesAsync(ct);

        var client = await clients.GetByIdAsync(clientId, ct);
        await alerts.CreateAndDispatchAsync(
            type: AlertType.ClientWaiting,
            severity: AlertSeverity.Info,
            title: "Novo chamado",
            message: $"{client?.Name ?? "Cliente"} abriu: {ticket.Title}",
            actionUrl: $"/chamados/{ticket.Id}",
            relatedEntityId: ticket.Id,
            relatedEntityType: nameof(Ticket),
            ct: ct);

        return Result<Guid>.Success(ticket.Id);
    }

    public async Task<Result> AddCommentAsync(
        Guid clientId, Guid ticketId, PortalAddCommentDto dto, string authorName, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(dto.Content))
            return Result.Failure("Mensagem vazia.");

        // Verifica que o ticket pertence ao cliente — proteção primária
        var owns = await tickets.AnyAsync(t => t.Id == ticketId && t.ClientId == clientId, ct);
        if (!owns) return Result.Failure("Chamado não encontrado.");

        var comment = new TicketComment
        {
            TicketId = ticketId,
            Content = dto.Content.Trim(),
            IsInternal = false,
            IsFromClient = true,
            AuthorName = authorName
        };
        await comments.AddAsync(comment, ct);
        await uow.SaveChangesAsync(ct);

        var ticket = await tickets.GetByIdAsync(ticketId, ct);
        await alerts.CreateAndDispatchAsync(
            type: AlertType.ClientWaiting,
            severity: AlertSeverity.Info,
            title: "Nova mensagem do cliente",
            message: $"{authorName} respondeu em {ticket?.Number}",
            actionUrl: $"/chamados/{ticketId}",
            relatedEntityId: ticketId,
            relatedEntityType: nameof(Ticket),
            ct: ct);

        return Result.Success();
    }

    private async Task<string> GenerateNumberAsync(CancellationToken ct)
    {
        var today = DateTime.UtcNow;
        var prefix = $"NX-{today:yyMMdd}-";
        var countToday = await tickets.Query().CountAsync(t => t.Number.StartsWith(prefix), ct);
        return $"{prefix}{(countToday + 1):D4}";
    }
}
