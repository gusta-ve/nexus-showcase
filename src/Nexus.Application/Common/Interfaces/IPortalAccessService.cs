using Nexus.Application.Common.Models;
using Nexus.Application.Features.Portal;

namespace Nexus.Application.Common.Interfaces;

public interface IPortalAccessService
{
    Task<Result<PortalAccessCreatedDto>> CreateClientAccessAsync(CreatePortalAccessDto dto, CancellationToken ct = default);
    Task<Result> ChangePasswordAsync(string userId, string currentPassword, string newPassword, CancellationToken ct = default);
    Task<PortalUserContext?> GetUserContextAsync(string userId, CancellationToken ct = default);

    /// <summary>UserId do dono do portal pra esse cliente (pra disparar notificação direcionada).</summary>
    Task<string?> GetUserIdForClientAsync(Guid clientId, CancellationToken ct = default);
}

public record PortalUserContext(Guid? ClientId, bool MustChangePassword, string FullName, string Email);
