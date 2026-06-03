using Microsoft.EntityFrameworkCore;
using Nexus.Application.Common.Interfaces;
using Nexus.Domain.Entities.Clients;

namespace Nexus.Application.Features.Portal;

public class PortalEquipmentItemDto
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Type { get; set; }
    public string? Brand { get; set; }
    public string? Model { get; set; }
    public string? OperatingSystem { get; set; }
    public string? Specs { get; set; }
    public string? Notes { get; set; }
    public DateTime? PurchaseDate { get; set; }
    public DateTime CreatedAt { get; set; }
}

public class PortalEquipmentService(IRepository<ClientEquipment> equipment)
{
    public async Task<List<PortalEquipmentItemDto>> GetMyEquipmentAsync(
        Guid clientId, CancellationToken ct = default)
    {
        return await equipment.Query()
            .Where(e => e.ClientId == clientId && e.IsActive)
            .OrderByDescending(e => e.CreatedAt)
            .Select(e => new PortalEquipmentItemDto
            {
                Id = e.Id,
                Name = e.Name,
                Type = e.Type,
                Brand = e.Brand,
                Model = e.Model,
                OperatingSystem = e.OperatingSystem,
                Specs = e.Specs,
                Notes = e.Notes,
                PurchaseDate = e.PurchaseDate,
                CreatedAt = e.CreatedAt
            })
            .ToListAsync(ct);
    }

    public async Task<PortalEquipmentItemDto?> GetByIdAsync(
        Guid id, Guid clientId, CancellationToken ct = default)
    {
        return await equipment.Query()
            .Where(e => e.Id == id && e.ClientId == clientId && e.IsActive)
            .Select(e => new PortalEquipmentItemDto
            {
                Id = e.Id,
                Name = e.Name,
                Type = e.Type,
                Brand = e.Brand,
                Model = e.Model,
                OperatingSystem = e.OperatingSystem,
                Specs = e.Specs,
                Notes = e.Notes,
                PurchaseDate = e.PurchaseDate,
                CreatedAt = e.CreatedAt
            })
            .FirstOrDefaultAsync(ct);
    }
}
