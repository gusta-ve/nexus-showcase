using Nexus.Domain.Common;

namespace Nexus.Domain.Entities.Clients;

public class ClientEquipment : AuditableEntity
{
    public Guid ClientId { get; set; }
    public Client Client { get; set; } = null!;

    public string Name { get; set; } = string.Empty;
    public string? Type { get; set; }
    public string? Brand { get; set; }
    public string? Model { get; set; }
    public string? SerialNumber { get; set; }
    public string? OperatingSystem { get; set; }
    public string? Specs { get; set; }
    public string? Notes { get; set; }
    public DateTime? PurchaseDate { get; set; }
    public bool IsActive { get; set; } = true;
}
