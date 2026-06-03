using Microsoft.EntityFrameworkCore;
using Nexus.Application.Common.Interfaces;
using Nexus.Application.Common.Models;
using Nexus.Domain.Entities.Financial;
using Nexus.Domain.Enums;

namespace Nexus.Application.Features.Financial;

public class TransactionService(
    IRepository<Transaction> transactions,
    IRepository<TransactionCategory> categories,
    IUnitOfWork uow)
{
    public async Task<PagedResult<TransactionListItemDto>> GetPagedAsync(
        TransactionType? type,
        IReadOnlyList<TransactionStatus>? statuses,
        Guid? categoryId,
        DateTime? from,
        DateTime? to,
        string? search,
        int page, int pageSize,
        CancellationToken ct = default)
    {
        page = page < 1 ? 1 : page;
        pageSize = pageSize is < 1 or > 100 ? 20 : pageSize;

        var query = transactions.Query();

        if (type.HasValue)
            query = query.Where(t => t.Type == type.Value);

        if (statuses is { Count: > 0 })
            query = query.Where(t => statuses.Contains(t.Status));

        if (categoryId.HasValue)
            query = query.Where(t => t.CategoryId == categoryId);

        if (from.HasValue)
            query = query.Where(t => t.DueDate >= from.Value);

        if (to.HasValue)
            query = query.Where(t => t.DueDate <= to.Value);

        if (!string.IsNullOrWhiteSpace(search))
        {
            var s = search.Trim().ToLower();
            query = query.Where(t =>
                t.Title.ToLower().Contains(s) ||
                (t.Client != null && t.Client.Name.ToLower().Contains(s)));
        }

        var total = await query.CountAsync(ct);

        var items = await query
            .OrderByDescending(t => t.DueDate)
            .ThenByDescending(t => t.CreatedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(t => new TransactionListItemDto
            {
                Id = t.Id,
                Title = t.Title,
                Amount = t.Amount,
                Type = t.Type,
                Status = t.Status,
                CategoryName = t.Category != null ? t.Category.Name : null,
                CategoryColor = t.Category != null ? t.Category.Color : null,
                ClientName = t.Client != null ? t.Client.Name : null,
                DueDate = t.DueDate,
                PaidAt = t.PaidAt,
                IsRecurring = t.IsRecurring,
                RecurrenceType = t.RecurrenceType
            })
            .ToListAsync(ct);

        return new PagedResult<TransactionListItemDto>
        {
            Items = items, TotalCount = total, Page = page, PageSize = pageSize
        };
    }

    public async Task<TransactionFormDto?> GetForEditAsync(Guid id, CancellationToken ct = default)
    {
        var t = await transactions.GetByIdAsync(id, ct);
        if (t is null) return null;
        return new TransactionFormDto
        {
            Id = t.Id,
            Title = t.Title,
            Description = t.Description,
            Amount = t.Amount,
            Type = t.Type,
            Status = t.Status,
            CategoryId = t.CategoryId,
            ClientId = t.ClientId,
            DueDate = t.DueDate,
            PaidAt = t.PaidAt,
            PaymentMethod = t.PaymentMethod,
            IsRecurring = t.IsRecurring,
            RecurrenceType = t.RecurrenceType,
            Notes = t.Notes
        };
    }

    public async Task<Result<Guid>> CreateAsync(TransactionFormDto dto, CancellationToken ct = default)
    {
        var v = Validate(dto);
        if (!v.Succeeded) return Result<Guid>.Failure(v.Error!);

        var t = new Transaction();
        Apply(dto, t);
        await transactions.AddAsync(t, ct);
        await uow.SaveChangesAsync(ct);
        return Result<Guid>.Success(t.Id);
    }

    public async Task<Result> UpdateAsync(Guid id, TransactionFormDto dto, CancellationToken ct = default)
    {
        var v = Validate(dto);
        if (!v.Succeeded) return v;

        var t = await transactions.GetByIdAsync(id, ct);
        if (t is null) return Result.Failure("Lançamento não encontrado.");

        Apply(dto, t);
        transactions.Update(t);
        await uow.SaveChangesAsync(ct);
        return Result.Success();
    }

    public async Task<Result> MarkAsPaidAsync(Guid id, string? paymentMethod, CancellationToken ct = default)
    {
        var t = await transactions.GetByIdAsync(id, ct);
        if (t is null) return Result.Failure("Lançamento não encontrado.");

        t.Status = TransactionStatus.Paid;
        t.PaidAt = DateTime.UtcNow;
        if (!string.IsNullOrWhiteSpace(paymentMethod))
            t.PaymentMethod = paymentMethod;

        transactions.Update(t);
        await uow.SaveChangesAsync(ct);
        return Result.Success();
    }

    public async Task<Result> DeleteAsync(Guid id, CancellationToken ct = default)
    {
        var t = await transactions.GetByIdAsync(id, ct);
        if (t is null) return Result.Failure("Lançamento não encontrado.");
        transactions.Remove(t);
        await uow.SaveChangesAsync(ct);
        return Result.Success();
    }

    public async Task<FinancialSummaryDto> GetMonthlySummaryAsync(CancellationToken ct = default)
    {
        var now = DateTime.UtcNow;
        var monthStart = new DateTime(now.Year, now.Month, 1, 0, 0, 0, DateTimeKind.Utc);
        var nextMonth = monthStart.AddMonths(1);

        var income = await transactions.Query()
            .Where(t => t.Type == TransactionType.Income &&
                        t.Status == TransactionStatus.Paid &&
                        t.PaidAt >= monthStart && t.PaidAt < nextMonth)
            .SumAsync(t => (decimal?)t.Amount, ct) ?? 0;

        var expenses = await transactions.Query()
            .Where(t => t.Type == TransactionType.Expense &&
                        t.Status == TransactionStatus.Paid &&
                        t.PaidAt >= monthStart && t.PaidAt < nextMonth)
            .SumAsync(t => (decimal?)t.Amount, ct) ?? 0;

        var pendReceive = await transactions.Query()
            .Where(t => t.Type == TransactionType.Income &&
                        (t.Status == TransactionStatus.Pending || t.Status == TransactionStatus.Overdue))
            .SumAsync(t => (decimal?)t.Amount, ct) ?? 0;

        var pendPay = await transactions.Query()
            .Where(t => t.Type == TransactionType.Expense &&
                        (t.Status == TransactionStatus.Pending || t.Status == TransactionStatus.Overdue))
            .SumAsync(t => (decimal?)t.Amount, ct) ?? 0;

        var overdue = await transactions.Query()
            .Where(t => t.Status != TransactionStatus.Paid &&
                        t.Status != TransactionStatus.Cancelled &&
                        t.DueDate < now)
            .SumAsync(t => (decimal?)t.Amount, ct) ?? 0;

        return new FinancialSummaryDto
        {
            MonthIncome = income,
            MonthExpenses = expenses,
            PendingReceivables = pendReceive,
            PendingPayables = pendPay,
            OverdueAmount = overdue
        };
    }

    public async Task<IReadOnlyList<CategoryDto>> GetCategoriesAsync(TransactionType? type = null, CancellationToken ct = default)
    {
        var q = categories.Query();
        if (type.HasValue) q = q.Where(c => c.Type == type.Value);
        return await q
            .OrderBy(c => c.Name)
            .Select(c => new CategoryDto { Id = c.Id, Name = c.Name, Color = c.Color, Type = c.Type })
            .ToListAsync(ct);
    }

    private static Result Validate(TransactionFormDto dto)
    {
        if (string.IsNullOrWhiteSpace(dto.Title))
            return Result.Failure("O título é obrigatório.");
        if (dto.Amount <= 0)
            return Result.Failure("O valor deve ser maior que zero.");
        if (dto.IsRecurring && dto.RecurrenceType == RecurrenceType.None)
            return Result.Failure("Defina o tipo de recorrência.");
        return Result.Success();
    }

    private static void Apply(TransactionFormDto dto, Transaction t)
    {
        t.Title = dto.Title.Trim();
        t.Description = dto.Description?.Trim();
        t.Amount = dto.Amount;
        t.Type = dto.Type;
        t.Status = dto.Status;
        t.CategoryId = dto.CategoryId;
        t.ClientId = dto.ClientId;
        t.DueDate = DateTime.SpecifyKind(dto.DueDate.Date, DateTimeKind.Utc);
        t.PaymentMethod = dto.PaymentMethod?.Trim();
        t.IsRecurring = dto.IsRecurring;
        t.RecurrenceType = dto.IsRecurring ? dto.RecurrenceType : RecurrenceType.None;
        t.Notes = dto.Notes?.Trim();

        // PaidAt: if status becomes Paid and no date, set now. If status off Paid, clear.
        if (t.Status == TransactionStatus.Paid)
            t.PaidAt = dto.PaidAt ?? DateTime.UtcNow;
        else
            t.PaidAt = null;
    }
}
