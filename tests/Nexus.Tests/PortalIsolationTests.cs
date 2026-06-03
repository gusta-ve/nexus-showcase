using Microsoft.EntityFrameworkCore;
using Nexus.Application.Common.Interfaces;
using Nexus.Application.Common.Models;
using Nexus.Application.Features.Notifications;
using Nexus.Application.Features.Portal;
using Nexus.Domain.Entities.Documents;
using Nexus.Domain.Enums;
using Nexus.Infrastructure.Persistence;
using Nexus.Infrastructure.Repositories;
using Xunit;

namespace Nexus.Tests;

/// <summary>
/// Isolamento multi-tenant: o portal NUNCA pode deixar um cliente acessar dado
/// de outro. Estes testes provam, ponta a ponta (PortalQuoteService + EF Core),
/// que o filtro por clientId impede IDOR — mesmo que o cliente "adivinhe" o id
/// do orçamento de outro cliente.
/// </summary>
public class PortalIsolationTests : IDisposable
{
    private static readonly Guid ClientA = Guid.NewGuid();
    private static readonly Guid ClientB = Guid.NewGuid();

    private readonly NexusDbContext _ctx;
    private readonly PortalQuoteService _sut;
    private readonly Guid _quoteOfA;
    private readonly Guid _quoteOfB;

    public PortalIsolationTests()
    {
        var options = new DbContextOptionsBuilder<NexusDbContext>()
            .UseInMemoryDatabase($"nexus-tests-{Guid.NewGuid()}")
            .Options;
        _ctx = new NexusDbContext(options);

        var qa = NewSentQuote(ClientA, "ORC-A");
        var qb = NewSentQuote(ClientB, "ORC-B");
        _ctx.Quotes.AddRange(qa, qb);
        _ctx.SaveChanges();
        _quoteOfA = qa.Id;
        _quoteOfB = qb.Id;

        _sut = new PortalQuoteService(new Repository<Quote>(_ctx), new TestUnitOfWork(_ctx), new NoopAlertService());
    }

    [Fact]
    public async Task Client_cannot_read_another_clients_quote()
    {
        // Cliente A pede o orçamento do cliente B (id "adivinhado") → nada.
        var result = await _sut.GetDetailAsync(_quoteOfB, ClientA);
        Assert.Null(result);
    }

    [Fact]
    public async Task Client_can_read_own_quote()
    {
        var result = await _sut.GetDetailAsync(_quoteOfA, ClientA);
        Assert.NotNull(result);
        Assert.Equal("ORC-A", result!.Number);
    }

    [Fact]
    public async Task Listing_returns_only_own_quotes()
    {
        var page = await _sut.GetMyQuotesAsync(ClientA, page: 1, pageSize: 50);
        Assert.Single(page.Items);
        Assert.Equal("ORC-A", page.Items[0].Number);
    }

    [Fact]
    public async Task Client_cannot_accept_another_clients_quote()
    {
        var result = await _sut.AcceptAsync(_quoteOfB, ClientA);
        Assert.False(result.Succeeded);

        // E o orçamento do B continua intocado (não foi aceito por ninguém de fora).
        var qb = await _ctx.Quotes.AsNoTracking().FirstAsync(x => x.Id == _quoteOfB);
        Assert.Equal(DocumentStatus.Sent, qb.Status);
    }

    [Fact]
    public async Task Draft_quotes_are_hidden_from_client()
    {
        var draft = NewSentQuote(ClientA, "ORC-DRAFT");
        draft.Status = DocumentStatus.Draft;
        _ctx.Quotes.Add(draft);
        await _ctx.SaveChangesAsync();

        // Rascunho é coisa de admin — o cliente não enxerga nem o próprio.
        var detail = await _sut.GetDetailAsync(draft.Id, ClientA);
        Assert.Null(detail);
    }

    private static Quote NewSentQuote(Guid clientId, string number) => new()
    {
        ClientId = clientId,
        Number = number,
        Title = "Orçamento de teste",
        Status = DocumentStatus.Sent,
        Subtotal = 1000m,
        Total = 1000m,
    };

    public void Dispose() => _ctx.Dispose();

    // ---------- test doubles ----------

    private sealed class TestUnitOfWork(NexusDbContext ctx) : IUnitOfWork
    {
        public IRepository<T> Repository<T>() where T : class => new Repository<T>(ctx);
        public Task<int> SaveChangesAsync(CancellationToken ct = default) => ctx.SaveChangesAsync(ct);
    }

    private sealed class NoopAlertService : IAlertService
    {
        public Task CreateAndDispatchAsync(AlertType type, AlertSeverity severity, string title, string? message,
            string? targetUserId = null, string? actionUrl = null, Guid? relatedEntityId = null,
            string? relatedEntityType = null, CancellationToken ct = default) => Task.CompletedTask;

        public Task<PagedResult<AlertListItemDto>> GetForUserAsync(string? userId, bool isAdmin, bool unreadOnly,
            int page, int pageSize, CancellationToken ct = default) => Task.FromResult(new PagedResult<AlertListItemDto>());

        public Task<int> GetUnreadCountAsync(string? userId, bool isAdmin, CancellationToken ct = default) => Task.FromResult(0);

        public Task<Result> MarkReadAsync(Guid id, CancellationToken ct = default) => Task.FromResult(Result.Success());

        public Task<Result> MarkAllReadAsync(string? userId, bool isAdmin, CancellationToken ct = default) => Task.FromResult(Result.Success());
    }
}
