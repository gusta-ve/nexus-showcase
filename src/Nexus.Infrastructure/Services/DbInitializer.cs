using System.Security.Cryptography;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Nexus.Domain.Entities.Financial;
using Nexus.Domain.Entities.Identity;
using Nexus.Domain.Enums;
using Nexus.Infrastructure.Persistence;

namespace Nexus.Infrastructure.Services;

public static class DbInitializer
{
    public static async Task InitializeAsync(IServiceProvider sp)
    {
        using var scope = sp.CreateScope();
        var s = scope.ServiceProvider;
        var logger = s.GetRequiredService<ILogger<NexusDbContext>>();
        var ctx = s.GetRequiredService<NexusDbContext>();
        var users = s.GetRequiredService<UserManager<ApplicationUser>>();
        var roles = s.GetRequiredService<RoleManager<IdentityRole>>();
        var config = s.GetRequiredService<IConfiguration>();

        try
        {
            await ctx.Database.MigrateAsync();

            foreach (var role in new[] { "Admin", "Technician", "Client" })
                if (!await roles.RoleExistsAsync(role))
                    await roles.CreateAsync(new IdentityRole(role));

            var adminEmail = config["Identity:AdminEmail"];
            if (string.IsNullOrWhiteSpace(adminEmail))
            {
                logger.LogWarning("Identity:AdminEmail not configured — skipping admin seed.");
            }
            else
            {
                // Password MUST come from configuration/secret. No default ever ships.
                var adminPassword = config["Identity:AdminPassword"];
                if (string.IsNullOrWhiteSpace(adminPassword))
                {
                    adminPassword = GenerateStrongPassword();
                    logger.LogWarning(
                        "Identity:AdminPassword not configured. A random password was generated " +
                        "for {Email}: {Password} — store it now; it is not shown again.",
                        adminEmail, adminPassword);
                }

                var admin = await users.FindByEmailAsync(adminEmail);
                if (admin is null)
                {
                    admin = new ApplicationUser
                    {
                        UserName = adminEmail,
                        Email = adminEmail,
                        FullName = "Gustavo Almeida",
                        EmailConfirmed = true,
                        IsActive = true
                    };
                    var r = await users.CreateAsync(admin, adminPassword);
                    if (r.Succeeded)
                        await users.AddToRoleAsync(admin, "Admin");
                    else
                        logger.LogError("Failed to create admin: {Errors}",
                            string.Join("; ", r.Errors.Select(e => e.Description)));
                }
                else
                {
                    // Enforce configured password (overwrites any known default).
                    // Safe today: there is no in-app password-change UI yet.
                    var token = await users.GeneratePasswordResetTokenAsync(admin);
                    var reset = await users.ResetPasswordAsync(admin, token, adminPassword);
                    if (!reset.Succeeded)
                        logger.LogError("Failed to reset admin password: {Errors}",
                            string.Join("; ", reset.Errors.Select(e => e.Description)));
                    if (!await users.IsInRoleAsync(admin, "Admin"))
                        await users.AddToRoleAsync(admin, "Admin");
                }
            }

            if (!await ctx.TransactionCategories.AnyAsync())
            {
                ctx.TransactionCategories.AddRange(
                    new TransactionCategory { Name = "Suporte Técnico", Color = "#6366f1", Type = TransactionType.Income },
                    new TransactionCategory { Name = "Consultoria", Color = "#06b6d4", Type = TransactionType.Income },
                    new TransactionCategory { Name = "Desenvolvimento", Color = "#22c55e", Type = TransactionType.Income },
                    new TransactionCategory { Name = "Manutenção Recorrente", Color = "#f59e0b", Type = TransactionType.Income },
                    new TransactionCategory { Name = "Infraestrutura / VPS", Color = "#ef4444", Type = TransactionType.Expense },
                    new TransactionCategory { Name = "Ferramentas e Software", Color = "#8b5cf6", Type = TransactionType.Expense },
                    new TransactionCategory { Name = "Equipamentos", Color = "#ec4899", Type = TransactionType.Expense });
                await ctx.SaveChangesAsync();
            }

            logger.LogInformation("Database initialized successfully.");
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Database initialization failed.");
            throw;
        }
    }

    private static string GenerateStrongPassword()
    {
        const string chars = "ABCDEFGHJKLMNPQRSTUVWXYZabcdefghijkmnpqrstuvwxyz23456789!@#$%&*";
        var bytes = RandomNumberGenerator.GetBytes(20);
        return "Nx" + new string(bytes.Select(b => chars[b % chars.Length]).ToArray());
    }
}
