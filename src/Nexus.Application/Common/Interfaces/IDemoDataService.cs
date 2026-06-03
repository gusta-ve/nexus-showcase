namespace Nexus.Application.Common.Interfaces;

/// <summary>Quantos registros de demonstração existem na base.</summary>
public record DemoDataStatus(bool Present, int Clients, int Total);

/// <summary>
/// Popula/remove um conjunto fictício de dados (PMEs de suporte/infra) para
/// screenshots de portfólio e para a instância de demo. Tudo é marcado com
/// CreatedBy = "demo-seed", então a remoção é cirúrgica e não toca dados reais.
/// </summary>
public interface IDemoDataService
{
    Task<DemoDataStatus> GetStatusAsync(CancellationToken ct = default);

    /// <summary>Cria os dados (idempotente: se já houver, não duplica).</summary>
    Task<DemoDataStatus> SeedAsync(CancellationToken ct = default);

    /// <summary>Remove apenas o que está marcado como demonstração. Retorna quantos registros saíram.</summary>
    Task<int> PurgeAsync(CancellationToken ct = default);
}
