using Microsoft.EntityFrameworkCore;
using Nexus.Application.Common.Interfaces;
using Nexus.Domain.Entities.Clients;
using Nexus.Domain.Entities.Tickets;
using Nexus.Domain.Enums;

namespace Nexus.Application.Features.Portal;

public class PortalDashboardService(IRepository<Ticket> tickets, IRepository<Client> clients)
{
    public async Task<PortalDashboardDto> GetAsync(Guid clientId, CancellationToken ct = default)
    {
        var client = await clients.GetByIdAsync(clientId, ct);

        var myTickets = tickets.Query().Where(t => t.ClientId == clientId);

        var dto = new PortalDashboardDto
        {
            ClientName = client?.Name ?? "Cliente",
            OpenTickets = await myTickets.CountAsync(t =>
                t.Status == TicketStatus.Open || t.Status == TicketStatus.Analyzing, ct),
            InProgressTickets = await myTickets.CountAsync(t =>
                t.Status == TicketStatus.InProgress || t.Status == TicketStatus.WaitingClient, ct),
            ResolvedTickets = await myTickets.CountAsync(t =>
                t.Status == TicketStatus.Resolved || t.Status == TicketStatus.Closed, ct),
            TotalTickets = await myTickets.CountAsync(ct),

            RecentTickets = await myTickets
                .OrderByDescending(t => t.CreatedAt)
                .Take(5)
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
                .ToListAsync(ct)
        };

        return dto;
    }
}
