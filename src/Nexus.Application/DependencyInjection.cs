using Microsoft.Extensions.DependencyInjection;
using Nexus.Application.Features.Clients;
using Nexus.Application.Features.Dashboard;
using Nexus.Application.Features.Financial;
using Nexus.Application.Features.Automation;
using Nexus.Application.Features.Knowledge;
using Nexus.Application.Features.Notes;
using Nexus.Application.Features.Portal;
using Nexus.Application.Features.Quotes;
using Nexus.Application.Features.Reviews;
using Nexus.Application.Features.Servers;
using Nexus.Application.Features.ServiceOrders;
using Nexus.Application.Features.Tasks;
using Nexus.Application.Features.Tickets;
using Nexus.Application.Features.Vault;

namespace Nexus.Application;

public static class DependencyInjection
{
    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        services.AddScoped<DashboardService>();
        services.AddScoped<ClientService>();
        services.AddScoped<TicketService>();
        services.AddScoped<TransactionService>();
        services.AddScoped<VaultService>();
        services.AddScoped<PortalDashboardService>();
        services.AddScoped<PortalTicketService>();
        services.AddScoped<PortalQuoteService>();
        services.AddScoped<PortalServiceOrderService>();
        services.AddScoped<PortalEquipmentService>();
        services.AddScoped<ServersService>();
        services.AddScoped<WorkItemService>();
        services.AddScoped<NoteService>();
        services.AddScoped<QuoteService>();
        services.AddScoped<ServiceOrderService>();
        services.AddScoped<KnowledgeService>();
        services.AddScoped<ReviewService>();
        services.AddScoped<AutomationService>();
        return services;
    }
}
