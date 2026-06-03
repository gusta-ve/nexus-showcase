using System.ComponentModel.DataAnnotations;
using Nexus.Domain.Enums;

namespace Nexus.Application.Features.Servers;

public class ServerListItemDto
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Hostname { get; set; }
    public string? PublicIp { get; set; }
    public ServerType Type { get; set; }
    public ServerStatus Status { get; set; }
    public string? Provider { get; set; }
    public string? Tags { get; set; }
    public bool MonitoringEnabled { get; set; }
    public DateTime? LastCheckedAt { get; set; }
    public decimal? MonthlyCost { get; set; }
    public DateTime? RenewalDate { get; set; }
    public int ServicesCount { get; set; }
}

public class ServerDetailDto
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Hostname { get; set; }
    public string? IpAddress { get; set; }
    public string? PublicIp { get; set; }
    public ServerType Type { get; set; }
    public ServerStatus Status { get; set; }
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
    public List<ServerServiceDto> Services { get; set; } = [];
}

public class ServerServiceDto
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Port { get; set; }
    public string? Protocol { get; set; }
    public string? Description { get; set; }
    public string? Url { get; set; }
    public bool IsPublic { get; set; }
    public bool IsRunning { get; set; }
}

public class ServerFormDto
{
    public Guid? Id { get; set; }

    [Required(ErrorMessage = "Informe o nome")]
    [MaxLength(200)]
    public string Name { get; set; } = string.Empty;

    [MaxLength(200)]
    public string? Hostname { get; set; }

    [MaxLength(45)]
    public string? IpAddress { get; set; }

    [MaxLength(45)]
    public string? PublicIp { get; set; }

    public ServerType Type { get; set; } = ServerType.VPS;
    public string? OperatingSystem { get; set; }
    public string? Provider { get; set; }
    public string? Location { get; set; }

    [Range(0, 256)]
    public int? CpuCores { get; set; }
    [Range(0, 4096)]
    public int? RamGb { get; set; }
    [Range(0, 1048576)]
    public int? StorageGb { get; set; }

    [MaxLength(10)]
    public string? SshPort { get; set; }
    [MaxLength(100)]
    public string? SshUser { get; set; }

    [MaxLength(500)]
    public string? Tags { get; set; }

    public string? Notes { get; set; }

    public bool MonitoringEnabled { get; set; }
    public string? HealthCheckUrl { get; set; }

    [Range(0, 999999)]
    public decimal? MonthlyCost { get; set; }
    public DateTime? RenewalDate { get; set; }
}

public class ServerServiceFormDto
{
    public Guid? Id { get; set; }
    public Guid ServerEntryId { get; set; }

    [Required(ErrorMessage = "Nome do serviço")]
    [MaxLength(200)]
    public string Name { get; set; } = string.Empty;

    [MaxLength(10)]
    public string? Port { get; set; }

    [MaxLength(20)]
    public string? Protocol { get; set; }

    public string? Description { get; set; }
    public string? Url { get; set; }
    public bool IsPublic { get; set; }
    public bool IsRunning { get; set; } = true;
}

public class ServersSummaryDto
{
    public int Total { get; set; }
    public int Online { get; set; }
    public int Offline { get; set; }
    public int Unknown { get; set; }
    public int RenewingSoon { get; set; }
    public decimal MonthlyCostTotal { get; set; }
}
