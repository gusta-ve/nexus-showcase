using Nexus.Domain.Common;
using Nexus.Domain.Entities.Documents;
using Nexus.Domain.Entities.Reviews;
using Nexus.Domain.Entities.Tickets;
using Nexus.Domain.Enums;

namespace Nexus.Domain.Entities.Clients;

public class Client : AuditableEntity, ISoftDeletable
{
    public string Name { get; set; } = string.Empty;
    public string? Email { get; set; }
    public string? Phone { get; set; }
    public string? WhatsApp { get; set; }
    public string? Document { get; set; }
    public ClientType Type { get; set; } = ClientType.Individual;
    public ClientStatus Status { get; set; } = ClientStatus.Active;

    public string? CompanyName { get; set; }
    public string? Address { get; set; }
    public string? City { get; set; }
    public string? State { get; set; }
    public string? ZipCode { get; set; }

    public string? Notes { get; set; }
    public string? Tags { get; set; }
    public bool IsRecurring { get; set; }
    public decimal? MonthlyValue { get; set; }

    public bool IsDeleted { get; set; }
    public DateTime? DeletedAt { get; set; }

    public ICollection<Ticket> Tickets { get; set; } = [];
    public ICollection<ClientEquipment> Equipment { get; set; } = [];
    public ICollection<ServiceOrder> ServiceOrders { get; set; } = [];
    public ICollection<Quote> Quotes { get; set; } = [];
    public ICollection<ClientReview> Reviews { get; set; } = [];
}
