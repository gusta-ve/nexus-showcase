using Nexus.Domain.Common;
using Nexus.Domain.Enums;

namespace Nexus.Domain.Entities.Servers;

public class ServerEntry : AuditableEntity, ISoftDeletable
{
    public string Name { get; set; } = string.Empty;
    public string? Hostname { get; set; }
    public string? IpAddress { get; set; }
    public string? PublicIp { get; set; }
    public ServerType Type { get; set; } = ServerType.VPS;
    public ServerStatus Status { get; set; } = ServerStatus.Unknown;

    public string? OperatingSystem { get; set; }
    public string? Provider { get; set; }
    public string? Location { get; set; }
    public int? CpuCores { get; set; }
    public int? RamGb { get; set; }
    public int? StorageGb { get; set; }

    public string? SshPort { get; set; }
    public string? SshUser { get; set; }
    public string? Tags { get; set; }
    public string? Notes { get; set; }

    public bool MonitoringEnabled { get; set; }
    public string? HealthCheckUrl { get; set; }
    public DateTime? LastCheckedAt { get; set; }

    public decimal? MonthlyCost { get; set; }
    public DateTime? RenewalDate { get; set; }

    public bool IsDeleted { get; set; }
    public DateTime? DeletedAt { get; set; }

    public ICollection<ServerService> Services { get; set; } = [];
}
