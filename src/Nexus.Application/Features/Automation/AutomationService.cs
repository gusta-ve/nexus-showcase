using Microsoft.EntityFrameworkCore;
using Nexus.Application.Common.Interfaces;
using Nexus.Domain.Entities.Automation;

namespace Nexus.Application.Features.Automation;

public class AutomationLogDto
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Source { get; set; }
    public string Status { get; set; } = "Pending";
    public string? TriggerType { get; set; }
    public string? ErrorMessage { get; set; }
    public int? DurationMs { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? CompletedAt { get; set; }
}

public class AutomationService
{
    private readonly IUnitOfWork _uow;

    public AutomationService(IUnitOfWork uow) => _uow = uow;

    public async Task<List<AutomationLogDto>> GetLogsAsync(int limit = 200)
    {
        return await _uow.Repository<AutomationLog>().Query()
            .OrderByDescending(x => x.CreatedAt)
            .Take(limit)
            .Select(x => new AutomationLogDto
            {
                Id = x.Id,
                Name = x.Name,
                Source = x.Source,
                Status = x.Status,
                TriggerType = x.TriggerType,
                ErrorMessage = x.ErrorMessage,
                DurationMs = x.DurationMs,
                CreatedAt = x.CreatedAt,
                CompletedAt = x.CompletedAt
            })
            .ToListAsync();
    }

    public async Task<AutomationLog?> GetDetailAsync(Guid id)
        => await _uow.Repository<AutomationLog>().GetByIdAsync(id);

    public async Task<AutomationLog> LogInboundAsync(string name, string source, string? payload)
    {
        var log = new AutomationLog
        {
            Name = name,
            Source = source,
            Status = "Success",
            TriggerType = "webhook.inbound",
            Payload = payload,
            CompletedAt = DateTime.UtcNow
        };
        await _uow.Repository<AutomationLog>().AddAsync(log);
        await _uow.SaveChangesAsync();
        return log;
    }

    public async Task DeleteAsync(Guid id)
    {
        var log = await _uow.Repository<AutomationLog>().GetByIdAsync(id);
        if (log is null) return;
        _uow.Repository<AutomationLog>().Remove(log);
        await _uow.SaveChangesAsync();
    }

    public async Task ClearAllAsync()
    {
        var all = await _uow.Repository<AutomationLog>().GetAllAsync();
        foreach (var log in all)
            _uow.Repository<AutomationLog>().Remove(log);
        await _uow.SaveChangesAsync();
    }
}
