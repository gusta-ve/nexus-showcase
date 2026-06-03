namespace Nexus.Domain.Common;

public interface IDomainEvent
{
    DateTime OccurredOn { get; }
}
