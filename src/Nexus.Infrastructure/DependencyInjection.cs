using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Nexus.Application.Common.Interfaces;
using Nexus.Domain.Entities.Identity;
using Nexus.Infrastructure.Persistence;
using Nexus.Infrastructure.Persistence.Interceptors;
using Nexus.Infrastructure.Repositories;
using Nexus.Infrastructure.Services;

namespace Nexus.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration config)
    {
        var connectionString = config.GetConnectionString("DefaultConnection")
            ?? throw new InvalidOperationException("Connection string 'DefaultConnection' not found.");

        services.AddScoped<AuditableEntityInterceptor>();

        services.AddDbContext<NexusDbContext>((sp, options) =>
        {
            options.UseNpgsql(connectionString, npgsql =>
            {
                npgsql.MigrationsAssembly(typeof(NexusDbContext).Assembly.FullName);
                npgsql.EnableRetryOnFailure(3);
            });
            options.AddInterceptors(sp.GetRequiredService<AuditableEntityInterceptor>());
        });

        // Factory paralela pra cenários que precisam de context isolado
        // (ex: leituras concorrentes no Blazor onde layout + page batem ao mesmo tempo)
        services.AddDbContextFactory<NexusDbContext>((sp, options) =>
        {
            options.UseNpgsql(connectionString, npgsql =>
            {
                npgsql.MigrationsAssembly(typeof(NexusDbContext).Assembly.FullName);
                npgsql.EnableRetryOnFailure(3);
            });
            options.AddInterceptors(sp.GetRequiredService<AuditableEntityInterceptor>());
        }, lifetime: ServiceLifetime.Scoped);

        services.AddIdentity<ApplicationUser, IdentityRole>(o =>
        {
            o.Password.RequireDigit = true;
            o.Password.RequiredLength = 8;
            o.Password.RequireNonAlphanumeric = false;
            o.Password.RequireUppercase = false;
            o.SignIn.RequireConfirmedAccount = false;
            o.User.RequireUniqueEmail = true;
        })
        .AddEntityFrameworkStores<NexusDbContext>()
        .AddDefaultTokenProviders();

        // Fail closed: reject empty or obvious template/example values so a misconfigured
        // deploy never silently encrypts the vault with a publicly-known placeholder key.
        static bool IsPlaceholderSecret(string value) =>
            new[] { "change", "troque", "example", "placeholder", "secret_here", "your_" }
                .Any(marker => value.Contains(marker, StringComparison.OrdinalIgnoreCase));

        var encryptionKey = config["Security:EncryptionKey"];
        if (string.IsNullOrWhiteSpace(encryptionKey) || IsPlaceholderSecret(encryptionKey))
            throw new InvalidOperationException(
                "Security:EncryptionKey is not configured with a real secret. " +
                "Generate one with `openssl rand -base64 32` and set it via the Security__EncryptionKey environment variable.");
        services.AddSingleton<IEncryptionService>(new EncryptionService(encryptionKey));

        services.AddScoped(typeof(IRepository<>), typeof(Repository<>));
        services.AddScoped<IUnitOfWork, UnitOfWork>();
        services.AddScoped<IPortalAccessService, PortalAccessService>();
        services.AddScoped<IDemoDataService, DemoDataService>();
        services.AddScoped<Nexus.Application.Features.Notifications.IAlertService, AlertService>();
        services.AddScoped<IEmailService, SmtpEmailService>();

        // Background worker: health check periódico dos servidores monitorados
        services.AddHostedService<ServerHealthCheckWorker>();

        return services;
    }
}
