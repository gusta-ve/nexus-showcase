using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.Identity;
using Nexus.Domain.Entities.Identity;

namespace Nexus.Web.Endpoints;

public static class AuthEndpoints
{
    public static void MapAuthEndpoints(this WebApplication app)
    {
        var group = app.MapGroup("/auth");

        group.MapPost("/login", async (
            HttpContext http,
            IAntiforgery antiforgery,
            SignInManager<ApplicationUser> signInManager,
            UserManager<ApplicationUser> userManager) =>
        {
            try { await antiforgery.ValidateRequestAsync(http); }
            catch { return Results.Redirect("/login?error=1"); }

            var form = await http.Request.ReadFormAsync();
            var email = form["email"].ToString();
            var password = form["password"].ToString();

            var user = await userManager.FindByEmailAsync(email);
            if (user is null || !user.IsActive)
                return Results.Redirect("/login?error=1");

            // Cookie sempre persistente (com Expires + MaxAge) — sem isso, vira
            // session-only e browsers como Brave/Safari isolam por aba: o usuário
            // abre uma aba nova e parece "deslogado". ExpireTimeSpan no
            // ConfigureApplicationCookie define quanto tempo (14d com sliding).
            var result = await signInManager.PasswordSignInAsync(user, password, isPersistent: true, lockoutOnFailure: true);
            if (!result.Succeeded)
                return Results.Redirect("/login?error=1");

            user.LastLoginAt = DateTime.UtcNow;
            await userManager.UpdateAsync(user);

            // Route by role: clientes vão pro portal, admin/tech pro dashboard.
            var isClient = await userManager.IsInRoleAsync(user, "Client");
            var landing = isClient ? "/portal" : "/dashboard";

            // Se o cliente precisa trocar senha no primeiro acesso, direciona já.
            if (isClient && user.MustChangePassword)
                landing = "/portal/trocar-senha";

            return Results.Redirect(landing);
        }).RequireRateLimiting("login");

        group.MapPost("/logout", async (HttpContext ctx, IAntiforgery antiforgery, SignInManager<ApplicationUser> signInManager) =>
        {
            try { await antiforgery.ValidateRequestAsync(ctx); }
            catch { return Results.Redirect("/login"); }
            await signInManager.SignOutAsync();
            return Results.Redirect("/login");
        });
    }
}
