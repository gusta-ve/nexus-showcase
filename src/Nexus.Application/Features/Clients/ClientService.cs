using Microsoft.EntityFrameworkCore;
using Nexus.Application.Common.Interfaces;
using Nexus.Application.Common.Models;
using Nexus.Domain.Entities.Clients;

namespace Nexus.Application.Features.Clients;

public class ClientService(IRepository<Client> clients, IUnitOfWork uow)
{
    public async Task<PagedResult<ClientListItemDto>> GetPagedAsync(
        string? search, int page, int pageSize, CancellationToken ct = default)
    {
        page = page < 1 ? 1 : page;
        pageSize = pageSize is < 1 or > 100 ? 20 : pageSize;

        var query = clients.Query();

        if (!string.IsNullOrWhiteSpace(search))
        {
            var s = search.Trim().ToLower();
            query = query.Where(c =>
                c.Name.ToLower().Contains(s) ||
                (c.Email != null && c.Email.ToLower().Contains(s)) ||
                (c.WhatsApp != null && c.WhatsApp.Contains(s)) ||
                (c.CompanyName != null && c.CompanyName.ToLower().Contains(s)));
        }

        var total = await query.CountAsync(ct);

        var items = await query
            .OrderByDescending(c => c.CreatedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(c => new ClientListItemDto
            {
                Id = c.Id,
                Name = c.Name,
                Email = c.Email,
                WhatsApp = c.WhatsApp,
                Phone = c.Phone,
                Type = c.Type,
                Status = c.Status,
                IsRecurring = c.IsRecurring,
                MonthlyValue = c.MonthlyValue,
                CreatedAt = c.CreatedAt
            })
            .ToListAsync(ct);

        return new PagedResult<ClientListItemDto>
        {
            Items = items,
            TotalCount = total,
            Page = page,
            PageSize = pageSize
        };
    }

    public async Task<ClientFormDto?> GetForEditAsync(Guid id, CancellationToken ct = default)
    {
        var c = await clients.GetByIdAsync(id, ct);
        if (c is null) return null;

        return new ClientFormDto
        {
            Id = c.Id,
            Name = c.Name,
            Email = c.Email,
            Phone = c.Phone,
            WhatsApp = c.WhatsApp,
            Document = c.Document,
            Type = c.Type,
            Status = c.Status,
            CompanyName = c.CompanyName,
            Address = c.Address,
            City = c.City,
            State = c.State,
            ZipCode = c.ZipCode,
            Notes = c.Notes,
            Tags = c.Tags,
            IsRecurring = c.IsRecurring,
            MonthlyValue = c.MonthlyValue
        };
    }

    public async Task<Result<Guid>> CreateAsync(ClientFormDto dto, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(dto.Name))
            return Result<Guid>.Failure("O nome é obrigatório.");

        var client = new Client();
        Apply(dto, client);

        await clients.AddAsync(client, ct);
        await uow.SaveChangesAsync(ct);

        return Result<Guid>.Success(client.Id);
    }

    public async Task<Result> UpdateAsync(Guid id, ClientFormDto dto, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(dto.Name))
            return Result.Failure("O nome é obrigatório.");

        var client = await clients.GetByIdAsync(id, ct);
        if (client is null)
            return Result.Failure("Cliente não encontrado.");

        Apply(dto, client);
        clients.Update(client);
        await uow.SaveChangesAsync(ct);

        return Result.Success();
    }

    public async Task<Result> DeleteAsync(Guid id, CancellationToken ct = default)
    {
        var client = await clients.GetByIdAsync(id, ct);
        if (client is null)
            return Result.Failure("Cliente não encontrado.");

        clients.Remove(client); // soft-delete via interceptor
        await uow.SaveChangesAsync(ct);

        return Result.Success();
    }

    private static void Apply(ClientFormDto dto, Client c)
    {
        c.Name = dto.Name.Trim();
        c.Email = dto.Email?.Trim();
        c.Phone = dto.Phone?.Trim();
        c.WhatsApp = dto.WhatsApp?.Trim();
        c.Document = dto.Document?.Trim();
        c.Type = dto.Type;
        c.Status = dto.Status;
        c.CompanyName = dto.CompanyName?.Trim();
        c.Address = dto.Address?.Trim();
        c.City = dto.City?.Trim();
        c.State = dto.State?.Trim();
        c.ZipCode = dto.ZipCode?.Trim();
        c.Notes = dto.Notes?.Trim();
        c.Tags = dto.Tags?.Trim();
        c.IsRecurring = dto.IsRecurring;
        c.MonthlyValue = dto.IsRecurring ? dto.MonthlyValue : null;
    }
}
