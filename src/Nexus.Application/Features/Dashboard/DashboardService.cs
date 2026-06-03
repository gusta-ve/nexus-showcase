using Microsoft.EntityFrameworkCore;
using Nexus.Application.Common.Interfaces;
using Nexus.Domain.Entities.Alerts;
using Nexus.Domain.Entities.Clients;
using Nexus.Domain.Entities.Financial;
using Nexus.Domain.Entities.Reviews;
using Nexus.Domain.Entities.Servers;
using Nexus.Domain.Entities.Tasks;
using Nexus.Domain.Entities.Tickets;
using Nexus.Domain.Enums;

namespace Nexus.Application.Features.Dashboard;

public class DashboardService(
    IRepository<Ticket> tickets,
    IRepository<Transaction> transactions,
    IRepository<Alert> alerts,
    IRepository<ServerEntry> servers,
    IRepository<Client> clients,
    IRepository<WorkItem> workItems,
    IRepository<ClientReview> reviews)
{
    public async Task<DashboardDto> GetAsync(CancellationToken ct = default)
    {
        var now = DateTime.UtcNow;
        var monthStart = new DateTime(now.Year, now.Month, 1, 0, 0, 0, DateTimeKind.Utc);

        var dto = new DashboardDto
        {
            OpenTickets = await tickets.CountAsync(t => t.Status == TicketStatus.Open || t.Status == TicketStatus.Analyzing, ct),
            TicketsInProgress = await tickets.CountAsync(t => t.Status == TicketStatus.InProgress, ct),
            ResolvedToday = await tickets.CountAsync(t => t.ResolvedAt != null && t.ResolvedAt.Value.Date == now.Date, ct),
            CriticalTickets = await tickets.CountAsync(t => t.Priority == TicketPriority.Critical
                && t.Status != TicketStatus.Resolved && t.Status != TicketStatus.Closed, ct),

            TotalClients = await clients.CountAsync(null, ct),
            ActiveClients = await clients.CountAsync(c => c.Status == ClientStatus.Active, ct),
            RecurringClients = await clients.CountAsync(c => c.IsRecurring, ct),

            PendingTasks = await workItems.CountAsync(w => w.Status == WorkItemStatus.Todo || w.Status == WorkItemStatus.InProgress, ct),
            OverdueTasks = await workItems.CountAsync(w => w.DueDate != null && w.DueDate < now
                && w.Status != WorkItemStatus.Done && w.Status != WorkItemStatus.Cancelled, ct),

            UnreadAlerts = await alerts.CountAsync(a => !a.IsRead, ct),
            ServersOnline = await servers.CountAsync(s => s.Status == ServerStatus.Online, ct),
            ServersOffline = await servers.CountAsync(s => s.Status == ServerStatus.Offline, ct),
        };

        var lastMonthStart = monthStart.AddMonths(-1);

        dto.MonthlyIncome = await transactions.Query()
            .Where(t => t.Type == TransactionType.Income && t.Status == TransactionStatus.Paid && t.PaidAt >= monthStart)
            .SumAsync(t => (decimal?)t.Amount, ct) ?? 0;

        dto.MonthlyExpenses = await transactions.Query()
            .Where(t => t.Type == TransactionType.Expense && t.Status == TransactionStatus.Paid && t.PaidAt >= monthStart)
            .SumAsync(t => (decimal?)t.Amount, ct) ?? 0;

        dto.IncomeLastMonth = await transactions.Query()
            .Where(t => t.Type == TransactionType.Income && t.Status == TransactionStatus.Paid
                     && t.PaidAt >= lastMonthStart && t.PaidAt < monthStart)
            .SumAsync(t => (decimal?)t.Amount, ct) ?? 0;

        dto.PendingReceivables = await transactions.Query()
            .Where(t => t.Type == TransactionType.Income && t.Status == TransactionStatus.Pending)
            .SumAsync(t => (decimal?)t.Amount, ct) ?? 0;

        dto.RecurringRevenue = await clients.Query()
            .Where(c => c.IsRecurring && c.MonthlyValue != null)
            .SumAsync(c => c.MonthlyValue ?? 0, ct);

        dto.RecentTickets = await tickets.Query()
            .OrderByDescending(t => t.CreatedAt)
            .Take(8)
            .Select(t => new RecentTicketDto
            {
                Id = t.Id,
                Number = t.Number,
                Title = t.Title,
                ClientName = t.Client != null ? t.Client.Name : t.ContactName,
                Status = t.Status.ToString(),
                Priority = t.Priority.ToString(),
                CreatedAt = t.CreatedAt
            })
            .ToListAsync(ct);

        dto.RecentAlerts = await alerts.Query()
            .Where(a => !a.IsRead)
            .OrderByDescending(a => a.CreatedAt)
            .Take(5)
            .Select(a => new AlertSummaryDto
            {
                Id = a.Id,
                Title = a.Title,
                Severity = a.Severity.ToString(),
                CreatedAt = a.CreatedAt
            })
            .ToListAsync(ct);

        var raw = await transactions.Query()
            .Where(t => t.PaidAt >= now.AddMonths(-5) && t.Status == TransactionStatus.Paid)
            .Select(t => new { t.PaidAt, t.Type, t.Amount })
            .ToListAsync(ct);

        dto.RevenueChart = Enumerable.Range(-5, 6)
            .Select(now.AddMonths)
            .Select(d => new MonthlyRevenueDto
            {
                Month = d.ToString("MMM/yy"),
                Income = raw.Where(r => r.PaidAt!.Value.Year == d.Year && r.PaidAt.Value.Month == d.Month
                    && r.Type == TransactionType.Income).Sum(r => r.Amount),
                Expenses = raw.Where(r => r.PaidAt!.Value.Year == d.Year && r.PaidAt.Value.Month == d.Month
                    && r.Type == TransactionType.Expense).Sum(r => r.Amount)
            })
            .ToList();

        // Tempo médio de resolução (últimos 30 dias) — em horas
        var thirtyDaysAgo = now.AddDays(-30);
        var resolvedRecent = await tickets.Query()
            .Where(t => t.ResolvedAt != null && t.ResolvedAt >= thirtyDaysAgo)
            .Select(t => new { t.CreatedAt, t.ResolvedAt })
            .ToListAsync(ct);
        if (resolvedRecent.Count > 0)
        {
            dto.AvgResolutionHours = Math.Round(
                resolvedRecent.Average(r => (r.ResolvedAt!.Value - r.CreatedAt).TotalHours), 1);
        }

        // NPS médio
        var ratings = await reviews.Query().Select(r => r.Rating).ToListAsync(ct);
        dto.ReviewCount = ratings.Count;
        if (ratings.Count > 0) dto.AvgRating = Math.Round(ratings.Average(), 2);

        // Top categorias de chamados (últimos 90 dias)
        var ninetyDaysAgo = now.AddDays(-90);
        var byCat = await tickets.Query()
            .Where(t => t.CreatedAt >= ninetyDaysAgo)
            .GroupBy(t => t.Category)
            .Select(g => new CategoryStatDto { Category = g.Key.ToString(), Count = g.Count() })
            .OrderByDescending(x => x.Count)
            .Take(4)
            .ToListAsync(ct);
        dto.TopCategories = byCat;

        return dto;
    }
}
