using System.Security.Cryptography;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Nexus.Application.Common.Interfaces;
using Nexus.Application.Common.Models;
using Nexus.Application.Features.Portal;
using Nexus.Domain.Entities.Identity;
using Nexus.Infrastructure.Persistence;

namespace Nexus.Infrastructure.Services;

public class PortalAccessService(
    UserManager<ApplicationUser> users,
    NexusDbContext ctx,
    IDbContextFactory<NexusDbContext> dbFactory) : IPortalAccessService
{
    public async Task<Result<PortalAccessCreatedDto>> CreateClientAccessAsync(
        CreatePortalAccessDto dto, CancellationToken ct = default)
    {
        if (dto.ClientId == Guid.Empty)
            return Result<PortalAccessCreatedDto>.Failure("Cliente inválido.");
        if (string.IsNullOrWhiteSpace(dto.Email))
            return Result<PortalAccessCreatedDto>.Failure("Email é obrigatório.");

        var clientExists = await ctx.Clients.AnyAsync(c => c.Id == dto.ClientId, ct);
        if (!clientExists)
            return Result<PortalAccessCreatedDto>.Failure("Cliente não encontrado.");

        var existing = await users.FindByEmailAsync(dto.Email);
        if (existing is not null)
        {
            // Se já existe, vincula esse user ao cliente (caso seja um pré-cadastro)
            if (existing.ClientId is not null && existing.ClientId != dto.ClientId)
                return Result<PortalAccessCreatedDto>.Failure(
                    "Já existe usuário com esse email vinculado a outro cliente.");

            // Gera nova senha temporária e força troca
            var token = await users.GeneratePasswordResetTokenAsync(existing);
            var tempPwd = GenerateTempPassword();
            var reset = await users.ResetPasswordAsync(existing, token, tempPwd);
            if (!reset.Succeeded)
                return Result<PortalAccessCreatedDto>.Failure(string.Join("; ", reset.Errors.Select(e => e.Description)));

            existing.ClientId = dto.ClientId;
            existing.MustChangePassword = true;
            existing.IsActive = true;
            await users.UpdateAsync(existing);

            if (!await users.IsInRoleAsync(existing, "Client"))
                await users.AddToRoleAsync(existing, "Client");

            return Result<PortalAccessCreatedDto>.Success(new PortalAccessCreatedDto
            {
                Email = existing.Email!,
                TemporaryPassword = tempPwd
            });
        }

        // Cria novo
        var client = await ctx.Clients.FirstAsync(c => c.Id == dto.ClientId, ct);
        var newUser = new ApplicationUser
        {
            UserName = dto.Email.Trim(),
            Email = dto.Email.Trim(),
            EmailConfirmed = true,
            FullName = client.Name,
            ClientId = dto.ClientId,
            MustChangePassword = true,
            IsActive = true
        };
        var tempPassword = GenerateTempPassword();
        var create = await users.CreateAsync(newUser, tempPassword);
        if (!create.Succeeded)
            return Result<PortalAccessCreatedDto>.Failure(string.Join("; ", create.Errors.Select(e => e.Description)));

        await users.AddToRoleAsync(newUser, "Client");

        return Result<PortalAccessCreatedDto>.Success(new PortalAccessCreatedDto
        {
            Email = newUser.Email!,
            TemporaryPassword = tempPassword
        });
    }

    public async Task<Result> ChangePasswordAsync(
        string userId, string currentPassword, string newPassword, CancellationToken ct = default)
    {
        var user = await users.FindByIdAsync(userId);
        if (user is null) return Result.Failure("Usuário não encontrado.");

        var change = await users.ChangePasswordAsync(user, currentPassword, newPassword);
        if (!change.Succeeded)
            return Result.Failure(string.Join("; ", change.Errors.Select(e => e.Description)));

        user.MustChangePassword = false;
        await users.UpdateAsync(user);
        return Result.Success();
    }

    public async Task<PortalUserContext?> GetUserContextAsync(string userId, CancellationToken ct = default)
    {
        // Usa context isolado via factory: layout e page do portal chamam este método
        // em paralelo no OnInitializedAsync, e DbContext scoped não é thread-safe.
        await using var db = await dbFactory.CreateDbContextAsync(ct);
        var u = await db.Users
            .Where(x => x.Id == userId)
            .Select(x => new { x.ClientId, x.MustChangePassword, x.FullName, x.Email })
            .FirstOrDefaultAsync(ct);
        if (u is null) return null;
        return new PortalUserContext(u.ClientId, u.MustChangePassword, u.FullName, u.Email ?? "");
    }

    public async Task<string?> GetUserIdForClientAsync(Guid clientId, CancellationToken ct = default)
    {
        await using var db = await dbFactory.CreateDbContextAsync(ct);
        return await db.Users
            .Where(x => x.ClientId == clientId && x.IsActive)
            .Select(x => x.Id)
            .FirstOrDefaultAsync(ct);
    }

    private static string GenerateTempPassword()
    {
        const string chars = "ABCDEFGHJKMNPQRSTUVWXYZabcdefghjkmnpqrstuvwxyz23456789";
        var bytes = RandomNumberGenerator.GetBytes(14);
        return "Cli" + new string(bytes.Select(b => chars[b % chars.Length]).ToArray()) + "9!";
    }
}
