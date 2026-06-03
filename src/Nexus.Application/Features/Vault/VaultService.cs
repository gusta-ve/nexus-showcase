using System.Security.Cryptography;
using Microsoft.EntityFrameworkCore;
using Nexus.Application.Common.Interfaces;
using Nexus.Application.Common.Models;
using Nexus.Domain.Entities.Vault;

namespace Nexus.Application.Features.Vault;

public class VaultService(
    IRepository<VaultEntry> entries,
    IUnitOfWork uow,
    IEncryptionService crypto)
{
    public async Task<PagedResult<VaultEntryListItemDto>> GetPagedAsync(
        string? search,
        string? category,
        bool favoritesOnly,
        int page, int pageSize,
        CancellationToken ct = default)
    {
        page = page < 1 ? 1 : page;
        pageSize = pageSize is < 1 or > 100 ? 20 : pageSize;

        var query = entries.Query();

        if (favoritesOnly)
            query = query.Where(e => e.IsFavorite);

        if (!string.IsNullOrWhiteSpace(category))
            query = query.Where(e => e.Category == category);

        if (!string.IsNullOrWhiteSpace(search))
        {
            var s = search.Trim().ToLower();
            query = query.Where(e =>
                e.Name.ToLower().Contains(s) ||
                (e.Username != null && e.Username.ToLower().Contains(s)) ||
                (e.Url != null && e.Url.ToLower().Contains(s)) ||
                (e.Tags != null && e.Tags.ToLower().Contains(s)));
        }

        var total = await query.CountAsync(ct);

        var items = await query
            .OrderByDescending(e => e.IsFavorite)
            .ThenBy(e => e.Name)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(e => new VaultEntryListItemDto
            {
                Id = e.Id,
                Name = e.Name,
                Category = e.Category,
                Username = e.Username,
                Url = e.Url,
                Tags = e.Tags,
                IsFavorite = e.IsFavorite,
                CreatedAt = e.CreatedAt,
                LastUsedAt = e.LastUsedAt,
                PasswordChangedAt = e.PasswordChangedAt
            })
            .ToListAsync(ct);

        return new PagedResult<VaultEntryListItemDto>
        {
            Items = items, TotalCount = total, Page = page, PageSize = pageSize
        };
    }

    public async Task<IReadOnlyList<string>> GetCategoriesAsync(CancellationToken ct = default)
    {
        return await entries.Query()
            .Where(e => e.Category != null && e.Category != "")
            .Select(e => e.Category!)
            .Distinct()
            .OrderBy(c => c)
            .ToListAsync(ct);
    }

    public async Task<VaultEntryFormDto?> GetForEditAsync(Guid id, CancellationToken ct = default)
    {
        var e = await entries.GetByIdAsync(id, ct);
        if (e is null) return null;
        return new VaultEntryFormDto
        {
            Id = e.Id,
            Name = e.Name,
            Category = e.Category,
            Username = e.Username,
            // NewPassword fica em branco — não devolvemos a senha decriptada
            // a menos que o usuário clique explicitamente em "Revelar".
            Url = e.Url,
            Tags = e.Tags,
            IsFavorite = e.IsFavorite
            // Notes propositalmente fora — re-decriptamos sob demanda (Reveal).
        };
    }

    public async Task<Result<Guid>> CreateAsync(VaultEntryFormDto dto, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(dto.Name))
            return Result<Guid>.Failure("O nome é obrigatório.");
        if (string.IsNullOrWhiteSpace(dto.NewPassword))
            return Result<Guid>.Failure("Informe a senha.");

        var entry = new VaultEntry
        {
            Name = dto.Name.Trim(),
            Category = dto.Category?.Trim(),
            Username = dto.Username?.Trim(),
            Url = dto.Url?.Trim(),
            Tags = dto.Tags?.Trim(),
            IsFavorite = dto.IsFavorite,
            EncryptedPassword = crypto.Encrypt(dto.NewPassword),
            EncryptedNotes = string.IsNullOrWhiteSpace(dto.Notes) ? null : crypto.Encrypt(dto.Notes),
            PasswordChangedAt = DateTime.UtcNow
        };

        await entries.AddAsync(entry, ct);
        await uow.SaveChangesAsync(ct);
        return Result<Guid>.Success(entry.Id);
    }

    public async Task<Result> UpdateAsync(Guid id, VaultEntryFormDto dto, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(dto.Name))
            return Result.Failure("O nome é obrigatório.");

        var entry = await entries.GetByIdAsync(id, ct);
        if (entry is null) return Result.Failure("Entrada não encontrada.");

        entry.Name = dto.Name.Trim();
        entry.Category = dto.Category?.Trim();
        entry.Username = dto.Username?.Trim();
        entry.Url = dto.Url?.Trim();
        entry.Tags = dto.Tags?.Trim();
        entry.IsFavorite = dto.IsFavorite;

        // Só sobrescreve a senha se o usuário forneceu uma nova
        if (!string.IsNullOrWhiteSpace(dto.NewPassword))
        {
            entry.EncryptedPassword = crypto.Encrypt(dto.NewPassword);
            entry.PasswordChangedAt = DateTime.UtcNow;
        }

        // Notas: se vazio explicitamente, limpa; se preenchido, re-encripta
        if (dto.Notes is null)
        {
            // mantém o que está (usuário não tocou)
        }
        else if (string.IsNullOrWhiteSpace(dto.Notes))
        {
            entry.EncryptedNotes = null;
        }
        else
        {
            entry.EncryptedNotes = crypto.Encrypt(dto.Notes);
        }

        entries.Update(entry);
        await uow.SaveChangesAsync(ct);
        return Result.Success();
    }

    public async Task<Result> ToggleFavoriteAsync(Guid id, CancellationToken ct = default)
    {
        var entry = await entries.GetByIdAsync(id, ct);
        if (entry is null) return Result.Failure("Entrada não encontrada.");
        entry.IsFavorite = !entry.IsFavorite;
        entries.Update(entry);
        await uow.SaveChangesAsync(ct);
        return Result.Success();
    }

    public async Task<Result<RevealedSecretDto>> RevealAsync(Guid id, CancellationToken ct = default)
    {
        var entry = await entries.GetByIdAsync(id, ct);
        if (entry is null) return Result<RevealedSecretDto>.Failure("Entrada não encontrada.");

        string password, notes = string.Empty;
        try
        {
            password = crypto.Decrypt(entry.EncryptedPassword);
            if (!string.IsNullOrEmpty(entry.EncryptedNotes))
                notes = crypto.Decrypt(entry.EncryptedNotes);
        }
        catch
        {
            return Result<RevealedSecretDto>.Failure(
                "Falha ao descriptografar — a chave de criptografia atual não corresponde à que cifrou esta entrada.");
        }

        // Marca o uso (auditoria simples) — separa transação curta
        entry.LastUsedAt = DateTime.UtcNow;
        entries.Update(entry);
        await uow.SaveChangesAsync(ct);

        return Result<RevealedSecretDto>.Success(new RevealedSecretDto
        {
            Name = entry.Name,
            Username = entry.Username,
            Password = password,
            Notes = string.IsNullOrEmpty(notes) ? null : notes,
            Url = entry.Url
        });
    }

    public async Task<Result> DeleteAsync(Guid id, CancellationToken ct = default)
    {
        var entry = await entries.GetByIdAsync(id, ct);
        if (entry is null) return Result.Failure("Entrada não encontrada.");
        entries.Remove(entry);
        await uow.SaveChangesAsync(ct);
        return Result.Success();
    }

    /// <summary>Gera senha forte criptograficamente segura. Default 20 chars, mistura caracteres.</summary>
    public static string GeneratePassword(int length = 20, bool includeSymbols = true)
    {
        if (length < 8) length = 8;
        if (length > 128) length = 128;

        const string lower = "abcdefghijkmnpqrstuvwxyz";
        const string upper = "ABCDEFGHJKLMNPQRSTUVWXYZ";
        const string digits = "23456789";
        const string symbols = "!@#$%&*?-_+=";

        var alphabet = lower + upper + digits + (includeSymbols ? symbols : "");
        var bytes = RandomNumberGenerator.GetBytes(length);
        var pwd = new char[length];
        for (var i = 0; i < length; i++) pwd[i] = alphabet[bytes[i] % alphabet.Length];

        // Garante pelo menos 1 de cada conjunto exigido (substituindo posições aleatórias deterministicamente)
        var required = new[] { lower, upper, digits };
        for (var i = 0; i < required.Length; i++)
        {
            var pos = bytes[i] % length;
            pwd[pos] = required[i][bytes[length - 1 - i] % required[i].Length];
        }
        if (includeSymbols)
        {
            var pos = bytes[3 % length] % length;
            pwd[pos] = symbols[bytes[(length - 4 + length) % length] % symbols.Length];
        }

        return new string(pwd);
    }
}
