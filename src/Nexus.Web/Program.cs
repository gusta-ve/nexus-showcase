using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.AspNetCore.RateLimiting;
using System.Threading.RateLimiting;
using MudBlazor.Services;
using QuestPDF.Infrastructure;
using Nexus.Application;
using Nexus.Application.Common.Interfaces;
using Nexus.Infrastructure;
using Nexus.Infrastructure.Services;
using Nexus.Web.Components;
using Nexus.Web.Endpoints;
using Nexus.Web.Services;
using Serilog;

Log.Logger = new LoggerConfiguration()
    .WriteTo.Console()
    .WriteTo.File("logs/nexus-.log", rollingInterval: RollingInterval.Day)
    .CreateBootstrapLogger();

// QuestPDF licença Community (gratuita para uso pessoal/empresa < US$1M/ano).
QuestPDF.Settings.License = LicenseType.Community;

// Cultura pt-BR em toda a aplicação: moeda como "R$ 1.234,50" (sem isso,
// CurrentCulture cai no invariante e ToString("C") vira "¤"); datas em português.
var ptBr = new System.Globalization.CultureInfo("pt-BR");
System.Globalization.CultureInfo.DefaultThreadCurrentCulture = ptBr;
System.Globalization.CultureInfo.DefaultThreadCurrentUICulture = ptBr;

try
{
    var builder = WebApplication.CreateBuilder(args);

    builder.Host.UseSerilog((ctx, cfg) => cfg
        .ReadFrom.Configuration(ctx.Configuration)
        .WriteTo.Console()
        .WriteTo.File("logs/nexus-.log", rollingInterval: RollingInterval.Day));

    builder.Services.AddInfrastructure(builder.Configuration);
    builder.Services.AddApplication();

    // Notificações real-time: usa um dispatcher in-memory simples.
    // Blazor Server já mantém um circuit WebSocket por usuário, então não precisamos
    // de SignalR cliente (que esbarrava em 401 porque a "conexão" sai do próprio servidor).
    builder.Services.AddSingleton<InMemoryRealtimeNotifier>();
    builder.Services.AddSingleton<IRealtimeNotifier>(sp => sp.GetRequiredService<InMemoryRealtimeNotifier>());
    builder.Services.AddSingleton<IRealtimeSubscriber>(sp => sp.GetRequiredService<InMemoryRealtimeNotifier>());

    builder.Services.AddRazorComponents()
        .AddInteractiveServerComponents();

    builder.Services.AddMudServices(c =>
    {
        c.SnackbarConfiguration.PositionClass = MudBlazor.Defaults.Classes.Position.BottomRight;
        c.SnackbarConfiguration.NewestOnTop = true;
        c.SnackbarConfiguration.ShowCloseIcon = true;
        c.SnackbarConfiguration.VisibleStateDuration = 4000;
    });

    builder.Services.AddCascadingAuthenticationState();
    builder.Services.AddHttpContextAccessor();
    builder.Services.AddScoped<ICurrentUserService, CurrentUserService>();

    // Rate limiting: máx 10 tentativas de login por IP a cada 5 minutos.
    // Identity já tem lockout por usuário; isto protege antes de chegar lá.
    builder.Services.AddRateLimiter(o =>
    {
        o.AddFixedWindowLimiter("login", cfg =>
        {
            cfg.Window = TimeSpan.FromMinutes(5);
            cfg.PermitLimit = 10;
            cfg.QueueLimit = 0;
            cfg.AutoReplenishment = true;
        });
        o.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
    });

    // DataProtection com path EXPLÍCITO + ApplicationName fixo.
    // Sem isso: keys vão pro home do user 'app' do container (que muda a cada rebuild)
    // ou ficam em memória → cookies emitidos antes ficam ilegíveis depois do deploy,
    // e o user precisa logar de novo a cada build. Volume Docker em /var/lib/nexus/dpkeys.
    builder.Services.AddDataProtection()
        .PersistKeysToFileSystem(new DirectoryInfo("/home/app/dpkeys"))
        .SetApplicationName("Nexus");

    // Behind Caddy/Let's Encrypt reverse proxy: trust X-Forwarded-* so the app
    // sees the original https scheme (correct redirects, secure cookies, antiforgery).
    builder.Services.Configure<ForwardedHeadersOptions>(o =>
    {
        o.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto;
        o.KnownIPNetworks.Clear();
        o.KnownProxies.Clear();
    });

    var behindTls = builder.Configuration.GetValue("Proxy:BehindTls", false);

    builder.Services.ConfigureApplicationCookie(o =>
    {
        o.LoginPath = "/login";
        o.LogoutPath = "/logout";
        o.AccessDeniedPath = "/login";
        o.ExpireTimeSpan = TimeSpan.FromDays(14);
        o.SlidingExpiration = true;
        o.Cookie.HttpOnly = true;
        o.Cookie.Name = "nexus_auth";
        o.Cookie.SameSite = SameSiteMode.Lax;   // explícito: navegação top-level GET vê o cookie
        o.Cookie.SecurePolicy = behindTls
            ? CookieSecurePolicy.Always
            : CookieSecurePolicy.SameAsRequest;
        o.Cookie.IsEssential = true;            // não vai parar em consent middleware
        o.Cookie.MaxAge = TimeSpan.FromDays(14); // força Max-Age no Set-Cookie (alguns browsers preferem Max-Age sobre Expires)
    });

    var app = builder.Build();

    app.UseForwardedHeaders();

    // Força pt-BR em cada request (moeda R$, datas em português) — robusto também
    // pro circuito Blazor, complementando o DefaultThreadCurrentCulture.
    app.UseRequestLocalization(new Microsoft.AspNetCore.Builder.RequestLocalizationOptions
    {
        DefaultRequestCulture = new Microsoft.AspNetCore.Localization.RequestCulture(ptBr),
        SupportedCultures = [ptBr],
        SupportedUICultures = [ptBr],
    });

    // O edge (docker/Caddyfile) já cobre HSTS, X-Content-Type-Options, X-Frame-Options
    // e Referrer-Policy. Aqui o Permissions-Policy e uma Content-Security-Policy completa.
    // 'unsafe-inline' em script é constraint do Blazor/MudBlazor (importmap + estilos
    // dinâmicos); o resto fecha bastante coisa. Via OnStarting pra sobrescrever o
    // frame-ancestors padrão do framework.
    var csp = "default-src 'self'; script-src 'self' 'unsafe-inline'; "
        + "style-src 'self' 'unsafe-inline' https://fonts.googleapis.com; "
        + "font-src 'self' https://fonts.gstatic.com data:; img-src 'self' data: https:; "
        + "connect-src 'self' wss: ws:; object-src 'none'; base-uri 'self'; "
        + "form-action 'self'; frame-ancestors 'self'";

    app.Use(async (context, next) =>
    {
        context.Response.Headers["Permissions-Policy"] = "geolocation=(), camera=(), microphone=(), payment=()";
        context.Response.OnStarting(() =>
        {
            context.Response.Headers["Content-Security-Policy"] = csp;
            return Task.CompletedTask;
        });
        await next();
    });

    if (!app.Environment.IsDevelopment())
    {
        app.UseExceptionHandler("/Error", createScopeForErrors: true);
        app.UseHsts();
    }

    app.UseSerilogRequestLogging();
    app.UseStaticFiles();
    app.UseRateLimiter();
    app.UseAuthentication();
    app.UseAuthorization();
    app.UseAntiforgery();

    // Redirects de "estou na página errada pra quem já tá logado".
    // Tratado em middleware (não no componente Razor) pra dar 302 HTTP limpo
    // ANTES do Blazor começar a streamar o prerender — sem flash de tela.
    app.Use(async (context, next) =>
    {
        if (context.Request.Method == "GET" && context.User?.Identity?.IsAuthenticated == true)
        {
            var path = context.Request.Path.Value ?? "";
            var isLogin = path.Equals("/login", StringComparison.OrdinalIgnoreCase);
            // raiz "/" também vai pro painel pra user logado.
            // bypass com ?landing=1 pra quem quer ver a landing pública (ex: link do NavMenu).
            var isRoot = path == "/" && !context.Request.Query.ContainsKey("landing");

            if (isLogin || isRoot)
            {
                var dest = context.User.IsInRole("Client") ? "/portal" : "/dashboard";
                context.Response.Redirect(dest);
                return;
            }
        }
        await next();
    });

    app.MapStaticAssets();
    app.MapRazorComponents<App>()
        .AddInteractiveServerRenderMode();

    app.MapAuthEndpoints();
    app.MapDocumentEndpoints();
    app.MapPortalDocumentEndpoints();
    app.MapWebhookEndpoints();

    await DbInitializer.InitializeAsync(app.Services);

    // Auto-seed de demonstração — SÓ na instância de demo (Seed:DemoData=true).
    // A produção mantém a flag desligada e usa os botões em /dados-demo.
    // Idempotente e isolado: nunca derruba o app.
    if (string.Equals(app.Configuration["Seed:DemoData"], "true", StringComparison.OrdinalIgnoreCase))
    {
        try
        {
            using var demoScope = app.Services.CreateScope();
            await demoScope.ServiceProvider
                .GetRequiredService<Nexus.Application.Common.Interfaces.IDemoDataService>()
                .SeedAsync();
        }
        catch (Exception seedEx)
        {
            Log.Error(seedEx, "Auto-seed de demonstração falhou — app segue normalmente.");
        }
    }

    app.Run();
}
catch (Exception ex)
{
    Log.Fatal(ex, "Nexus failed to start");
}
finally
{
    Log.CloseAndFlush();
}
