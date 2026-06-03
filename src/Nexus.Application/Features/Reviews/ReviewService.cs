using Microsoft.EntityFrameworkCore;
using Nexus.Application.Common.Interfaces;
using Nexus.Application.Common.Models;
using Nexus.Application.Features.Notifications;
using Nexus.Domain.Entities.Reviews;
using Nexus.Domain.Entities.Tickets;
using Nexus.Domain.Enums;

namespace Nexus.Application.Features.Reviews;

public class ReviewService(
    IRepository<ClientReview> reviews,
    IRepository<Ticket> tickets,
    IUnitOfWork uow,
    IAlertService alerts)
{
    /// <summary>Cria uma avaliação. Chamado pelo portal cliente.</summary>
    public async Task<Result<Guid>> CreateAsync(
        Guid clientId, string reviewerName, CreateReviewDto dto, CancellationToken ct = default)
    {
        if (dto.Rating < 1 || dto.Rating > 5)
            return Result<Guid>.Failure("Avaliação deve ser de 1 a 5 estrelas.");

        // Valida que o ticket (se informado) pertence ao cliente
        if (dto.TicketId is { } tid)
        {
            var owns = await tickets.AnyAsync(t => t.Id == tid && t.ClientId == clientId, ct);
            if (!owns) return Result<Guid>.Failure("Chamado não encontrado.");

            // Evita duplicata
            var alreadyReviewed = await reviews.AnyAsync(r => r.TicketId == tid, ct);
            if (alreadyReviewed) return Result<Guid>.Failure("Esse chamado já foi avaliado.");
        }

        var review = new ClientReview
        {
            ClientId = clientId,
            TicketId = dto.TicketId,
            ReviewerName = reviewerName,
            Rating = dto.Rating,
            Comment = dto.Comment?.Trim(),
            IsApproved = false,    // admin precisa aprovar pra ir pra landing
            IsPublic = dto.AllowPublic
        };
        await reviews.AddAsync(review, ct);
        await uow.SaveChangesAsync(ct);

        // Notifica admin em tempo real
        await alerts.CreateAndDispatchAsync(
            type: AlertType.SystemEvent,
            severity: dto.Rating >= 4 ? AlertSeverity.Info : AlertSeverity.Warning,
            title: $"Nova avaliação · {dto.Rating}★",
            message: string.IsNullOrWhiteSpace(dto.Comment)
                ? $"{reviewerName} avaliou (sem comentário)"
                : $"{reviewerName}: \"{Truncate(dto.Comment, 80)}\"",
            actionUrl: "/avaliacoes",
            relatedEntityId: review.Id,
            relatedEntityType: nameof(ClientReview),
            ct: ct);

        return Result<Guid>.Success(review.Id);
    }

    public async Task<bool> HasReviewForTicketAsync(Guid ticketId, CancellationToken ct = default)
        => await reviews.AnyAsync(r => r.TicketId == ticketId, ct);

    // === Admin === //

    public async Task<PagedResult<ReviewListItemDto>> GetPagedAsync(
        bool? unapprovedOnly, int page, int pageSize, CancellationToken ct = default)
    {
        page = page < 1 ? 1 : page;
        pageSize = pageSize is < 1 or > 100 ? 20 : pageSize;

        var q = reviews.Query();
        if (unapprovedOnly == true) q = q.Where(r => !r.IsApproved);

        var total = await q.CountAsync(ct);
        var list = await q
            .OrderByDescending(r => r.CreatedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(r => new ReviewListItemDto
            {
                Id = r.Id,
                ReviewerName = r.ReviewerName,
                ClientName = r.Client != null ? r.Client.Name : null,
                TicketNumber = r.Ticket != null ? r.Ticket.Number : null,
                Rating = r.Rating,
                Comment = r.Comment,
                IsApproved = r.IsApproved,
                IsPublic = r.IsPublic,
                CreatedAt = r.CreatedAt
            })
            .ToListAsync(ct);

        return new PagedResult<ReviewListItemDto>
        {
            Items = list, TotalCount = total, Page = page, PageSize = pageSize
        };
    }

    public async Task<ReviewStatsDto> GetStatsAsync(CancellationToken ct = default)
    {
        var all = await reviews.Query().Select(r => r.Rating).ToListAsync(ct);
        if (all.Count == 0) return new ReviewStatsDto();
        return new ReviewStatsDto
        {
            Total = all.Count,
            Average = Math.Round(all.Average(), 2),
            Count5 = all.Count(r => r == 5),
            Count4 = all.Count(r => r == 4),
            Count3 = all.Count(r => r == 3),
            Count2 = all.Count(r => r == 2),
            Count1 = all.Count(r => r == 1)
        };
    }

    public async Task<Result> ToggleApprovedAsync(Guid id, CancellationToken ct = default)
    {
        var r = await reviews.GetByIdAsync(id, ct);
        if (r is null) return Result.Failure("Avaliação não encontrada.");
        r.IsApproved = !r.IsApproved;
        reviews.Update(r);
        await uow.SaveChangesAsync(ct);
        return Result.Success();
    }

    public async Task<Result> DeleteAsync(Guid id, CancellationToken ct = default)
    {
        var r = await reviews.GetByIdAsync(id, ct);
        if (r is null) return Result.Failure("Avaliação não encontrada.");
        reviews.Remove(r);
        await uow.SaveChangesAsync(ct);
        return Result.Success();
    }

    // === Público (landing) === //

    /// <summary>Devolve só reviews aprovadas + autorizadas + 4-5 estrelas pra mostrar na landing.</summary>
    public async Task<List<PublicReviewDto>> GetPublicAsync(int limit = 6, CancellationToken ct = default)
    {
        return await reviews.Query()
            .Where(r => r.IsApproved && r.IsPublic && r.Rating >= 4
                     && !string.IsNullOrEmpty(r.Comment))
            .OrderByDescending(r => r.CreatedAt)
            .Take(limit)
            .Select(r => new PublicReviewDto
            {
                Rating = r.Rating,
                Comment = r.Comment!,
                ReviewerName = r.ReviewerName,
                Where = r.Client != null ? r.Client.City : null,
                CreatedAt = r.CreatedAt
            })
            .ToListAsync(ct);
    }

    private static string Truncate(string s, int max)
        => s.Length <= max ? s : s.Substring(0, max - 1) + "…";
}
