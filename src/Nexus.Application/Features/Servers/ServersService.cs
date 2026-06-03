using Microsoft.EntityFrameworkCore;
using Nexus.Application.Common.Interfaces;
using Nexus.Application.Common.Models;
using Nexus.Domain.Entities.Servers;
using Nexus.Domain.Enums;

namespace Nexus.Application.Features.Servers;

public class ServersService(
    IRepository<ServerEntry> servers,
    IRepository<Domain.Entities.Servers.ServerService> services,
    IUnitOfWork uow)
{
    public async Task<PagedResult<ServerListItemDto>> GetPagedAsync(
        string? search,
        IReadOnlyList<ServerType>? types,
        IReadOnlyList<ServerStatus>? statuses,
        int page, int pageSize,
        CancellationToken ct = default)
    {
        page = page < 1 ? 1 : page;
        pageSize = pageSize is < 1 or > 100 ? 20 : pageSize;

        var q = servers.Query();

        if (types is { Count: > 0 })
            q = q.Where(s => types.Contains(s.Type));

        if (statuses is { Count: > 0 })
            q = q.Where(s => statuses.Contains(s.Status));

        if (!string.IsNullOrWhiteSpace(search))
        {
            var s = search.Trim().ToLower();
            q = q.Where(x =>
                x.Name.ToLower().Contains(s) ||
                (x.Hostname != null && x.Hostname.ToLower().Contains(s)) ||
                (x.PublicIp != null && x.PublicIp.Contains(s)) ||
                (x.IpAddress != null && x.IpAddress.Contains(s)) ||
                (x.Tags != null && x.Tags.ToLower().Contains(s)) ||
                (x.Provider != null && x.Provider.ToLower().Contains(s)));
        }

        var total = await q.CountAsync(ct);

        var items = await q
            .OrderBy(s => s.Status == ServerStatus.Offline ? 0 : 1)
            .ThenBy(s => s.Name)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(s => new ServerListItemDto
            {
                Id = s.Id,
                Name = s.Name,
                Hostname = s.Hostname,
                PublicIp = s.PublicIp,
                Type = s.Type,
                Status = s.Status,
                Provider = s.Provider,
                Tags = s.Tags,
                MonitoringEnabled = s.MonitoringEnabled,
                LastCheckedAt = s.LastCheckedAt,
                MonthlyCost = s.MonthlyCost,
                RenewalDate = s.RenewalDate,
                ServicesCount = s.Services.Count
            })
            .ToListAsync(ct);

        return new PagedResult<ServerListItemDto>
        {
            Items = items, TotalCount = total, Page = page, PageSize = pageSize
        };
    }

    public async Task<ServerDetailDto?> GetDetailAsync(Guid id, CancellationToken ct = default)
    {
        return await servers.Query()
            .Where(s => s.Id == id)
            .Select(s => new ServerDetailDto
            {
                Id = s.Id,
                Name = s.Name,
                Hostname = s.Hostname,
                IpAddress = s.IpAddress,
                PublicIp = s.PublicIp,
                Type = s.Type,
                Status = s.Status,
                OperatingSystem = s.OperatingSystem,
                Provider = s.Provider,
                Location = s.Location,
                CpuCores = s.CpuCores,
                RamGb = s.RamGb,
                StorageGb = s.StorageGb,
                SshPort = s.SshPort,
                SshUser = s.SshUser,
                Tags = s.Tags,
                Notes = s.Notes,
                MonitoringEnabled = s.MonitoringEnabled,
                HealthCheckUrl = s.HealthCheckUrl,
                LastCheckedAt = s.LastCheckedAt,
                MonthlyCost = s.MonthlyCost,
                RenewalDate = s.RenewalDate,
                Services = s.Services
                    .OrderBy(svc => svc.Name)
                    .Select(svc => new ServerServiceDto
                    {
                        Id = svc.Id,
                        Name = svc.Name,
                        Port = svc.Port,
                        Protocol = svc.Protocol,
                        Description = svc.Description,
                        Url = svc.Url,
                        IsPublic = svc.IsPublic,
                        IsRunning = svc.IsRunning
                    }).ToList()
            })
            .FirstOrDefaultAsync(ct);
    }

    public async Task<ServerFormDto?> GetForEditAsync(Guid id, CancellationToken ct = default)
    {
        var s = await servers.GetByIdAsync(id, ct);
        if (s is null) return null;
        return new ServerFormDto
        {
            Id = s.Id, Name = s.Name, Hostname = s.Hostname, IpAddress = s.IpAddress, PublicIp = s.PublicIp,
            Type = s.Type, OperatingSystem = s.OperatingSystem, Provider = s.Provider, Location = s.Location,
            CpuCores = s.CpuCores, RamGb = s.RamGb, StorageGb = s.StorageGb,
            SshPort = s.SshPort, SshUser = s.SshUser, Tags = s.Tags, Notes = s.Notes,
            MonitoringEnabled = s.MonitoringEnabled, HealthCheckUrl = s.HealthCheckUrl,
            MonthlyCost = s.MonthlyCost, RenewalDate = s.RenewalDate
        };
    }

    public async Task<Result<Guid>> CreateAsync(ServerFormDto dto, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(dto.Name))
            return Result<Guid>.Failure("O nome é obrigatório.");

        var server = new ServerEntry();
        Apply(dto, server);
        await servers.AddAsync(server, ct);
        await uow.SaveChangesAsync(ct);
        return Result<Guid>.Success(server.Id);
    }

    public async Task<Result> UpdateAsync(Guid id, ServerFormDto dto, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(dto.Name))
            return Result.Failure("O nome é obrigatório.");

        var s = await servers.GetByIdAsync(id, ct);
        if (s is null) return Result.Failure("Servidor não encontrado.");

        Apply(dto, s);
        servers.Update(s);
        await uow.SaveChangesAsync(ct);
        return Result.Success();
    }

    public async Task<Result> DeleteAsync(Guid id, CancellationToken ct = default)
    {
        var s = await servers.GetByIdAsync(id, ct);
        if (s is null) return Result.Failure("Servidor não encontrado.");
        servers.Remove(s);
        await uow.SaveChangesAsync(ct);
        return Result.Success();
    }

    public async Task<Result<Guid>> AddServiceAsync(ServerServiceFormDto dto, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(dto.Name))
            return Result<Guid>.Failure("Nome do serviço obrigatório.");
        if (dto.ServerEntryId == Guid.Empty)
            return Result<Guid>.Failure("Servidor inválido.");

        var owns = await servers.AnyAsync(s => s.Id == dto.ServerEntryId, ct);
        if (!owns) return Result<Guid>.Failure("Servidor não encontrado.");

        var svc = new Domain.Entities.Servers.ServerService();
        ApplyService(dto, svc);
        await services.AddAsync(svc, ct);
        await uow.SaveChangesAsync(ct);
        return Result<Guid>.Success(svc.Id);
    }

    public async Task<Result> UpdateServiceAsync(Guid id, ServerServiceFormDto dto, CancellationToken ct = default)
    {
        var svc = await services.GetByIdAsync(id, ct);
        if (svc is null) return Result.Failure("Serviço não encontrado.");
        ApplyService(dto, svc);
        services.Update(svc);
        await uow.SaveChangesAsync(ct);
        return Result.Success();
    }

    public async Task<Result> DeleteServiceAsync(Guid id, CancellationToken ct = default)
    {
        var svc = await services.GetByIdAsync(id, ct);
        if (svc is null) return Result.Failure("Serviço não encontrado.");
        services.Remove(svc);
        await uow.SaveChangesAsync(ct);
        return Result.Success();
    }

    public async Task<ServersSummaryDto> GetSummaryAsync(CancellationToken ct = default)
    {
        var now = DateTime.UtcNow;
        var thirtyDays = now.AddDays(30);

        return new ServersSummaryDto
        {
            Total = await servers.CountAsync(null, ct),
            Online = await servers.CountAsync(s => s.Status == ServerStatus.Online, ct),
            Offline = await servers.CountAsync(s => s.Status == ServerStatus.Offline, ct),
            Unknown = await servers.CountAsync(s => s.Status == ServerStatus.Unknown
                                                  || s.Status == ServerStatus.Maintenance, ct),
            RenewingSoon = await servers.CountAsync(s => s.RenewalDate != null
                                                         && s.RenewalDate <= thirtyDays
                                                         && s.RenewalDate >= now, ct),
            MonthlyCostTotal = await servers.Query()
                .Where(s => s.MonthlyCost != null)
                .SumAsync(s => s.MonthlyCost ?? 0, ct)
        };
    }

    private static void Apply(ServerFormDto dto, ServerEntry s)
    {
        s.Name = dto.Name.Trim();
        s.Hostname = dto.Hostname?.Trim();
        s.IpAddress = dto.IpAddress?.Trim();
        s.PublicIp = dto.PublicIp?.Trim();
        s.Type = dto.Type;
        s.OperatingSystem = dto.OperatingSystem?.Trim();
        s.Provider = dto.Provider?.Trim();
        s.Location = dto.Location?.Trim();
        s.CpuCores = dto.CpuCores;
        s.RamGb = dto.RamGb;
        s.StorageGb = dto.StorageGb;
        s.SshPort = dto.SshPort?.Trim();
        s.SshUser = dto.SshUser?.Trim();
        s.Tags = dto.Tags?.Trim();
        s.Notes = dto.Notes?.Trim();
        s.MonitoringEnabled = dto.MonitoringEnabled;
        s.HealthCheckUrl = dto.HealthCheckUrl?.Trim();
        s.MonthlyCost = dto.MonthlyCost;
        s.RenewalDate = dto.RenewalDate.HasValue
            ? DateTime.SpecifyKind(dto.RenewalDate.Value.Date, DateTimeKind.Utc)
            : null;
    }

    private static void ApplyService(ServerServiceFormDto dto, Domain.Entities.Servers.ServerService svc)
    {
        svc.ServerEntryId = dto.ServerEntryId;
        svc.Name = dto.Name.Trim();
        svc.Port = dto.Port?.Trim();
        svc.Protocol = dto.Protocol?.Trim();
        svc.Description = dto.Description?.Trim();
        svc.Url = dto.Url?.Trim();
        svc.IsPublic = dto.IsPublic;
        svc.IsRunning = dto.IsRunning;
    }
}
