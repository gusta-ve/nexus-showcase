using System.Linq.Expressions;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using Nexus.Domain.Common;
using Nexus.Domain.Entities.Alerts;
using Nexus.Domain.Entities.Automation;
using Nexus.Domain.Entities.Clients;
using Nexus.Domain.Entities.Documents;
using Nexus.Domain.Entities.Financial;
using Nexus.Domain.Entities.Identity;
using Nexus.Domain.Entities.Knowledge;
using Nexus.Domain.Entities.Notes;
using Nexus.Domain.Entities.Portfolio;
using Nexus.Domain.Entities.Reviews;
using Nexus.Domain.Entities.Servers;
using Nexus.Domain.Entities.Tasks;
using Nexus.Domain.Entities.Tickets;
using Nexus.Domain.Entities.Vault;

namespace Nexus.Infrastructure.Persistence;

public class NexusDbContext(DbContextOptions<NexusDbContext> options)
    : IdentityDbContext<ApplicationUser>(options)
{
    public DbSet<Client> Clients => Set<Client>();
    public DbSet<ClientEquipment> ClientEquipment => Set<ClientEquipment>();
    public DbSet<Ticket> Tickets => Set<Ticket>();
    public DbSet<TicketComment> TicketComments => Set<TicketComment>();
    public DbSet<TicketAttachment> TicketAttachments => Set<TicketAttachment>();
    public DbSet<TransactionCategory> TransactionCategories => Set<TransactionCategory>();
    public DbSet<Transaction> Transactions => Set<Transaction>();
    public DbSet<VaultEntry> VaultEntries => Set<VaultEntry>();
    public DbSet<ServerEntry> Servers => Set<ServerEntry>();
    public DbSet<ServerService> ServerServices => Set<ServerService>();
    public DbSet<KnowledgeArticle> KnowledgeArticles => Set<KnowledgeArticle>();
    public DbSet<Note> Notes => Set<Note>();
    public DbSet<WorkItem> WorkItems => Set<WorkItem>();
    public DbSet<PortfolioItem> PortfolioItems => Set<PortfolioItem>();
    public DbSet<PortfolioImage> PortfolioImages => Set<PortfolioImage>();
    public DbSet<Quote> Quotes => Set<Quote>();
    public DbSet<QuoteItem> QuoteItems => Set<QuoteItem>();
    public DbSet<ServiceOrder> ServiceOrders => Set<ServiceOrder>();
    public DbSet<ClientReview> ClientReviews => Set<ClientReview>();
    public DbSet<Alert> Alerts => Set<Alert>();
    public DbSet<AutomationLog> AutomationLogs => Set<AutomationLog>();

    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);
        builder.ApplyConfigurationsFromAssembly(typeof(NexusDbContext).Assembly);

        // Global soft-delete query filter
        foreach (var entityType in builder.Model.GetEntityTypes())
        {
            if (typeof(ISoftDeletable).IsAssignableFrom(entityType.ClrType))
            {
                var param = Expression.Parameter(entityType.ClrType, "e");
                var prop = Expression.Property(param, nameof(ISoftDeletable.IsDeleted));
                var filter = Expression.Lambda(Expression.Not(prop), param);
                builder.Entity(entityType.ClrType).HasQueryFilter(filter);
            }
        }
    }
}
