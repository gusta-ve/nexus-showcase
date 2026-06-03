namespace Nexus.Application.Features.Dashboard;

public class DashboardDto
{
    public int OpenTickets { get; set; }
    public int TicketsInProgress { get; set; }
    public int ResolvedToday { get; set; }
    public int CriticalTickets { get; set; }

    public int TotalClients { get; set; }
    public int ActiveClients { get; set; }
    public int RecurringClients { get; set; }

    public decimal MonthlyIncome { get; set; }
    public decimal MonthlyExpenses { get; set; }
    public decimal MonthlyProfit => MonthlyIncome - MonthlyExpenses;
    public decimal PendingReceivables { get; set; }
    public decimal RecurringRevenue { get; set; }

    public int PendingTasks { get; set; }
    public int OverdueTasks { get; set; }

    public int UnreadAlerts { get; set; }
    public int ServersOnline { get; set; }
    public int ServersOffline { get; set; }

    // KPIs de qualidade/eficiência (últimos 30 dias)
    public double? AvgResolutionHours { get; set; }   // média entre CreatedAt e ResolvedAt
    public double? AvgRating { get; set; }            // NPS médio
    public int ReviewCount { get; set; }              // total de reviews
    public decimal IncomeLastMonth { get; set; }      // pra calcular variação MoM
    public double IncomeChangePct =>
        IncomeLastMonth > 0 ? (double)((MonthlyIncome - IncomeLastMonth) / IncomeLastMonth) * 100 : 0;

    public List<RecentTicketDto> RecentTickets { get; set; } = [];
    public List<MonthlyRevenueDto> RevenueChart { get; set; } = [];
    public List<AlertSummaryDto> RecentAlerts { get; set; } = [];
    public List<CategoryStatDto> TopCategories { get; set; } = [];
}

public class CategoryStatDto
{
    public string Category { get; set; } = string.Empty;
    public int Count { get; set; }
}

public class RecentTicketDto
{
    public Guid Id { get; set; }
    public string Number { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string? ClientName { get; set; }
    public string Status { get; set; } = string.Empty;
    public string Priority { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
}

public class MonthlyRevenueDto
{
    public string Month { get; set; } = string.Empty;
    public decimal Income { get; set; }
    public decimal Expenses { get; set; }
}

public class AlertSummaryDto
{
    public Guid Id { get; set; }
    public string Title { get; set; } = string.Empty;
    public string Severity { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
}
