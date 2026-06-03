using System.Collections.Concurrent;
using Nexus.Application.Common.Interfaces;
using Nexus.Infrastructure.Persistence;

namespace Nexus.Infrastructure.Repositories;

public class UnitOfWork(NexusDbContext context) : IUnitOfWork
{
    private readonly ConcurrentDictionary<Type, object> _repositories = new();

    public IRepository<T> Repository<T>() where T : class
        => (IRepository<T>)_repositories.GetOrAdd(typeof(T), _ => new Repository<T>(context));

    public Task<int> SaveChangesAsync(CancellationToken ct = default)
        => context.SaveChangesAsync(ct);
}
