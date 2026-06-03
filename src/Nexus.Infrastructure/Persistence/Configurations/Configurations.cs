using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Nexus.Domain.Entities.Clients;
using Nexus.Domain.Entities.Documents;
using Nexus.Domain.Entities.Financial;
using Nexus.Domain.Entities.Identity;
using Nexus.Domain.Entities.Tickets;
using Nexus.Domain.Entities.Vault;

namespace Nexus.Infrastructure.Persistence.Configurations;

public class ApplicationUserConfiguration : IEntityTypeConfiguration<ApplicationUser>
{
    public void Configure(EntityTypeBuilder<ApplicationUser> b)
    {
        b.ToTable("users");
        b.Property(u => u.FullName).HasMaxLength(200).IsRequired();
        b.Property(u => u.Bio).HasMaxLength(1000);
    }
}

public class ClientConfiguration : IEntityTypeConfiguration<Client>
{
    public void Configure(EntityTypeBuilder<Client> b)
    {
        b.ToTable("clients");
        b.Property(c => c.Name).HasMaxLength(200).IsRequired();
        b.Property(c => c.Email).HasMaxLength(256);
        b.Property(c => c.Phone).HasMaxLength(30);
        b.Property(c => c.WhatsApp).HasMaxLength(30);
        b.Property(c => c.Document).HasMaxLength(20);
        b.Property(c => c.CompanyName).HasMaxLength(200);
        b.Property(c => c.Tags).HasMaxLength(500);
        b.Property(c => c.MonthlyValue).HasColumnType("numeric(12,2)");
        b.HasIndex(c => c.Name);
        b.HasIndex(c => c.Email);

        b.HasMany(c => c.Tickets).WithOne(t => t.Client!).HasForeignKey(t => t.ClientId).OnDelete(DeleteBehavior.SetNull);
        b.HasMany(c => c.Equipment).WithOne(e => e.Client).HasForeignKey(e => e.ClientId).OnDelete(DeleteBehavior.Cascade);
        b.HasMany(c => c.ServiceOrders).WithOne(s => s.Client!).HasForeignKey(s => s.ClientId).OnDelete(DeleteBehavior.SetNull);
        b.HasMany(c => c.Quotes).WithOne(q => q.Client!).HasForeignKey(q => q.ClientId).OnDelete(DeleteBehavior.SetNull);
        b.HasMany(c => c.Reviews).WithOne(r => r.Client!).HasForeignKey(r => r.ClientId).OnDelete(DeleteBehavior.SetNull);
    }
}

public class TicketConfiguration : IEntityTypeConfiguration<Ticket>
{
    public void Configure(EntityTypeBuilder<Ticket> b)
    {
        b.ToTable("tickets");
        b.Property(t => t.Number).HasMaxLength(20).IsRequired();
        b.Property(t => t.Title).HasMaxLength(500).IsRequired();
        b.Property(t => t.ServiceValue).HasColumnType("numeric(12,2)");
        b.Property(t => t.TrackingToken).HasMaxLength(64);
        b.HasIndex(t => t.Number).IsUnique();
        b.HasIndex(t => t.TrackingToken);
        b.HasIndex(t => t.Status);

        b.HasMany(t => t.Comments).WithOne(c => c.Ticket).HasForeignKey(c => c.TicketId).OnDelete(DeleteBehavior.Cascade);
        b.HasMany(t => t.Attachments).WithOne(a => a.Ticket).HasForeignKey(a => a.TicketId).OnDelete(DeleteBehavior.Cascade);
    }
}

public class TransactionConfiguration : IEntityTypeConfiguration<Transaction>
{
    public void Configure(EntityTypeBuilder<Transaction> b)
    {
        b.ToTable("transactions");
        b.Property(t => t.Title).HasMaxLength(300).IsRequired();
        b.Property(t => t.Amount).HasColumnType("numeric(12,2)").IsRequired();
        b.Property(t => t.PaymentMethod).HasMaxLength(50);
        b.HasIndex(t => t.DueDate);
        b.HasOne(t => t.Category).WithMany(c => c.Transactions)
            .HasForeignKey(t => t.CategoryId).OnDelete(DeleteBehavior.SetNull);
        b.HasOne(t => t.Client).WithMany()
            .HasForeignKey(t => t.ClientId).OnDelete(DeleteBehavior.SetNull);
    }
}

public class TransactionCategoryConfiguration : IEntityTypeConfiguration<TransactionCategory>
{
    public void Configure(EntityTypeBuilder<TransactionCategory> b)
    {
        b.ToTable("transaction_categories");
        b.Property(c => c.Name).HasMaxLength(100).IsRequired();
        b.Property(c => c.Color).HasMaxLength(20);
    }
}

public class VaultEntryConfiguration : IEntityTypeConfiguration<VaultEntry>
{
    public void Configure(EntityTypeBuilder<VaultEntry> b)
    {
        b.ToTable("vault_entries");
        b.Property(v => v.Name).HasMaxLength(200).IsRequired();
        b.Property(v => v.Category).HasMaxLength(100);
        b.Property(v => v.Username).HasMaxLength(200);
        b.HasIndex(v => v.Category);
    }
}

public class QuoteConfiguration : IEntityTypeConfiguration<Quote>
{
    public void Configure(EntityTypeBuilder<Quote> b)
    {
        b.ToTable("quotes");
        b.Property(q => q.Number).HasMaxLength(20).IsRequired();
        b.Property(q => q.Title).HasMaxLength(300).IsRequired();
        b.Property(q => q.Subtotal).HasColumnType("numeric(12,2)");
        b.Property(q => q.Discount).HasColumnType("numeric(12,2)");
        b.Property(q => q.Tax).HasColumnType("numeric(12,2)");
        b.Property(q => q.Total).HasColumnType("numeric(12,2)");
        b.HasIndex(q => q.Number).IsUnique();
        b.HasMany(q => q.Items).WithOne(i => i.Quote).HasForeignKey(i => i.QuoteId).OnDelete(DeleteBehavior.Cascade);
    }
}

public class QuoteItemConfiguration : IEntityTypeConfiguration<QuoteItem>
{
    public void Configure(EntityTypeBuilder<QuoteItem> b)
    {
        b.ToTable("quote_items");
        b.Property(i => i.Description).HasMaxLength(500).IsRequired();
        b.Property(i => i.UnitPrice).HasColumnType("numeric(12,2)");
        b.Property(i => i.Quantity).HasColumnType("numeric(12,2)");
        b.Property(i => i.Total).HasColumnType("numeric(12,2)");
    }
}

public class ServiceOrderConfiguration : IEntityTypeConfiguration<ServiceOrder>
{
    public void Configure(EntityTypeBuilder<ServiceOrder> b)
    {
        b.ToTable("service_orders");
        b.Property(s => s.Number).HasMaxLength(20).IsRequired();
        b.Property(s => s.Title).HasMaxLength(300).IsRequired();
        b.Property(s => s.LaborValue).HasColumnType("numeric(12,2)");
        b.Property(s => s.PartsValue).HasColumnType("numeric(12,2)");
        b.Property(s => s.Discount).HasColumnType("numeric(12,2)");
        b.Property(s => s.Total).HasColumnType("numeric(12,2)");
        b.HasIndex(s => s.Number).IsUnique();
        b.HasOne(s => s.Ticket).WithMany().HasForeignKey(s => s.TicketId).OnDelete(DeleteBehavior.SetNull);
    }
}
