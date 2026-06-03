using Nexus.Domain.Common;

namespace Nexus.Domain.Entities.Servers;

public class ServerService : BaseEntity
{
    public Guid ServerEntryId { get; set; }
    public ServerEntry ServerEntry { get; set; } = null!;

    public string Name { get; set; } = string.Empty;
    public string? Port { get; set; }
    public string? Protocol { get; set; }
    public string? Description { get; set; }
    public string? Url { get; set; }
    public bool IsPublic { get; set; }
    public bool IsRunning { get; set; }
}
