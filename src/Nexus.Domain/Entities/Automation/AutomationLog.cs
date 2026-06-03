using Nexus.Domain.Common;

namespace Nexus.Domain.Entities.Automation;

public class AutomationLog : BaseEntity
{
    public string Name { get; set; } = string.Empty;
    public string? Source { get; set; }
    public string Status { get; set; } = "Pending";
    public string? TriggerType { get; set; }
    public string? Payload { get; set; }
    public string? Result { get; set; }
    public string? ErrorMessage { get; set; }
    public int? DurationMs { get; set; }
    public DateTime? CompletedAt { get; set; }
}
