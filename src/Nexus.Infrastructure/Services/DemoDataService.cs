using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Nexus.Application.Common.Interfaces;
using Nexus.Domain.Entities.Alerts;
using Nexus.Domain.Entities.Clients;
using Nexus.Domain.Entities.Documents;
using Nexus.Domain.Entities.Financial;
using Nexus.Domain.Entities.Identity;
using Nexus.Domain.Entities.Knowledge;
using Nexus.Domain.Entities.Notes;
using Nexus.Domain.Entities.Reviews;
using Nexus.Domain.Entities.Servers;
using Nexus.Domain.Entities.Tasks;
using Nexus.Domain.Entities.Tickets;
using Nexus.Domain.Entities.Vault;
using Nexus.Domain.Enums;
using Nexus.Infrastructure.Persistence;

namespace Nexus.Infrastructure.Services;

/// <summary>
/// Popula/remove um conjunto fictício porém realista (PMEs brasileiras de
/// suporte/infra) para screenshots de portfólio e para a instância de demo.
///
/// • Tudo é marcado com CreatedBy = <see cref="Marker"/> (alertas via
///   RelatedEntityType) — a remoção é cirúrgica e jamais toca dados reais.
/// • <see cref="SeedAsync"/> é idempotente (não duplica).
/// • Usa um <see cref="NexusDbContext"/> PRÓPRIO, sem o AuditableEntityInterceptor,
///   para preservar os CreatedAt históricos (datas espalhadas em meses) e o
///   marcador — que o interceptor sobrescreveria para "agora"/usuário atual.
/// </summary>
public class DemoDataService(
    IConfiguration config,
    IEncryptionService enc,
    UserManager<ApplicationUser> users) : IDemoDataService
{
    public const string Marker = "demo-seed";

    private const string PortalEmail = "portal@horizontecontabil.com.br";
    private const string PortalContact = "Ricardo Menezes";
    private const string DefaultPortalPassword = "Portal@Demo2026";

    private static readonly string[] DemoAlertTitles =
        ["Consultoria LGPD vencida", "Servidor em manutenção", "Chamado aguardando cliente"];

    private NexusDbContext NewContext()
    {
        var cs = config.GetConnectionString("DefaultConnection")
            ?? throw new InvalidOperationException("Connection string 'DefaultConnection' não encontrada.");
        var options = new DbContextOptionsBuilder<NexusDbContext>()
            .UseNpgsql(cs, npgsql => npgsql.EnableRetryOnFailure(3))
            .Options;
        return new NexusDbContext(options);
    }

    // ───────────────────────────── Status ───────────────────────────────────────

    public async Task<DemoDataStatus> GetStatusAsync(CancellationToken ct = default)
    {
        await using var ctx = NewContext();
        return await ComputeStatusAsync(ctx, ct);
    }

    private static async Task<DemoDataStatus> ComputeStatusAsync(NexusDbContext ctx, CancellationToken ct)
    {
        var clients = await ctx.Clients.IgnoreQueryFilters().CountAsync(c => c.CreatedBy == Marker, ct);
        var total = clients
            + await ctx.Tickets.IgnoreQueryFilters().CountAsync(x => x.CreatedBy == Marker, ct)
            + await ctx.Quotes.IgnoreQueryFilters().CountAsync(x => x.CreatedBy == Marker, ct)
            + await ctx.ServiceOrders.IgnoreQueryFilters().CountAsync(x => x.CreatedBy == Marker, ct)
            + await ctx.Transactions.IgnoreQueryFilters().CountAsync(x => x.CreatedBy == Marker, ct)
            + await ctx.VaultEntries.IgnoreQueryFilters().CountAsync(x => x.CreatedBy == Marker, ct)
            + await ctx.ClientEquipment.CountAsync(x => x.CreatedBy == Marker, ct)
            + await ctx.Servers.IgnoreQueryFilters().CountAsync(x => x.CreatedBy == Marker, ct)
            + await ctx.WorkItems.IgnoreQueryFilters().CountAsync(x => x.CreatedBy == Marker, ct)
            + await ctx.KnowledgeArticles.IgnoreQueryFilters().CountAsync(x => x.CreatedBy == Marker, ct)
            + await ctx.Notes.IgnoreQueryFilters().CountAsync(x => x.CreatedBy == Marker, ct)
            + await ctx.ClientReviews.CountAsync(x => x.CreatedBy == Marker, ct);
        return new DemoDataStatus(clients > 0, clients, total);
    }

    // ───────────────────────────── Purge ────────────────────────────────────────

    public async Task<int> PurgeAsync(CancellationToken ct = default)
    {
        await using var ctx = NewContext();
        var removed = 0;

        // Filhos sem marcador (QuoteItem/ServerService) saem por cascade no banco.
        // Nenhuma FK aqui é Restrict, então a ordem não causa violação.
        removed += await ctx.Tickets.IgnoreQueryFilters().Where(x => x.CreatedBy == Marker).ExecuteDeleteAsync(ct);
        removed += await ctx.Quotes.IgnoreQueryFilters().Where(x => x.CreatedBy == Marker).ExecuteDeleteAsync(ct);
        removed += await ctx.ServiceOrders.IgnoreQueryFilters().Where(x => x.CreatedBy == Marker).ExecuteDeleteAsync(ct);
        removed += await ctx.ClientReviews.Where(x => x.CreatedBy == Marker).ExecuteDeleteAsync(ct);
        removed += await ctx.Transactions.IgnoreQueryFilters().Where(x => x.CreatedBy == Marker).ExecuteDeleteAsync(ct);
        removed += await ctx.ClientEquipment.Where(x => x.CreatedBy == Marker).ExecuteDeleteAsync(ct);
        removed += await ctx.Servers.IgnoreQueryFilters().Where(x => x.CreatedBy == Marker).ExecuteDeleteAsync(ct);
        removed += await ctx.WorkItems.IgnoreQueryFilters().Where(x => x.CreatedBy == Marker).ExecuteDeleteAsync(ct);
        removed += await ctx.VaultEntries.IgnoreQueryFilters().Where(x => x.CreatedBy == Marker).ExecuteDeleteAsync(ct);
        removed += await ctx.KnowledgeArticles.IgnoreQueryFilters().Where(x => x.CreatedBy == Marker).ExecuteDeleteAsync(ct);
        removed += await ctx.Notes.IgnoreQueryFilters().Where(x => x.CreatedBy == Marker).ExecuteDeleteAsync(ct);
        // Marcador atual + títulos do conjunto (cobre alertas legados sem marcador).
        removed += await ctx.Alerts
            .Where(x => x.RelatedEntityType == Marker || DemoAlertTitles.Contains(x.Title))
            .ExecuteDeleteAsync(ct);
        removed += await ctx.Clients.IgnoreQueryFilters().Where(x => x.CreatedBy == Marker).ExecuteDeleteAsync(ct);

        // Usuário do portal de demonstração
        var portal = await users.FindByEmailAsync(PortalEmail);
        if (portal is not null && await users.DeleteAsync(portal) is { Succeeded: true })
            removed++;

        return removed;
    }

    // ───────────────────────────── Seed ─────────────────────────────────────────

    public async Task<DemoDataStatus> SeedAsync(CancellationToken ct = default)
    {
        await using var ctx = NewContext();

        if (await ctx.Clients.IgnoreQueryFilters().AnyAsync(c => c.CreatedBy == Marker, ct))
            return await ComputeStatusAsync(ctx, ct); // idempotente

        var now = DateTime.UtcNow;
        var cats = await ctx.TransactionCategories.ToDictionaryAsync(c => c.Name, c => c.Id, ct);
        Guid? Cat(string name) => cats.TryGetValue(name, out var id) ? id : null;

        // ── Clientes ────────────────────────────────────────────────────────────
        var horizonte = NewClient("Contabilidade Horizonte Ltda", ClientType.Company, ClientStatus.Active,
            email: "contato@horizontecontabil.com.br", phone: "+55 11 3251-4400", whatsapp: "+55 11 99812-4400",
            doc: "18.452.119/0001-07", company: "Horizonte Serviços Contábeis", city: "São Paulo", state: "SP",
            recurring: true, monthly: 890m, tags: "contrato,mensalista,prioridade",
            notes: "Atendimento mensal de TI: 12 estações + servidor de arquivos. SLA prioritário.");
        var padaria = NewClient("Padaria Estrela Ltda", ClientType.Company, ClientStatus.Active,
            email: "financeiro@padariaestrela.com.br", phone: "+55 11 2876-1190", whatsapp: "+55 11 98123-7745",
            doc: "27.880.412/0001-55", company: "Padaria e Confeitaria Estrela", city: "Guarulhos", state: "SP",
            recurring: true, monthly: 450m, tags: "contrato,pdv,mensalista",
            notes: "Rede de PDVs + retaguarda. Reestruturação de rede concluída em maio.");
        var vidaplena = NewClient("Clínica Vida Plena", ClientType.Company, ClientStatus.Active,
            email: "ti@clinicavidaplena.com.br", phone: "+55 11 4002-8922", whatsapp: "+55 11 99655-2210",
            doc: "33.107.654/0001-90", company: "Clínica Vida Plena Saúde", city: "Santo André", state: "SP",
            recurring: true, monthly: 1200m, tags: "saude,lgpd,backup",
            notes: "Prontuário eletrônico — exigência de backup diário e conformidade LGPD.");
        var ramos = NewClient("Advocacia Ramos & Associados", ClientType.Company, ClientStatus.Active,
            email: "secretaria@ramosadvocacia.com.br", phone: "+55 11 3661-7012", whatsapp: "+55 11 98477-1188",
            doc: "09.554.330/0001-12", company: "Ramos & Associados Sociedade de Advogados", city: "São Paulo", state: "SP",
            recurring: false, monthly: null, tags: "juridico,vpn",
            notes: "VPN para acesso remoto aos autos. Demanda pontual de infra.");
        var veloz = NewClient("Auto Peças Veloz", ClientType.Company, ClientStatus.Active,
            email: "compras@autopecasveloz.com.br", phone: "+55 11 2201-9080", whatsapp: "+55 11 99300-4521",
            doc: "41.998.205/0001-33", company: "Veloz Distribuidora de Autopeças", city: "Osasco", state: "SP",
            recurring: false, monthly: null, tags: "erp,fiscal",
            notes: "Integração do ERP com SEFAZ. Emissão de NF-e instável antes do ajuste.");
        var pilates = NewClient("Studio Pilates Equilíbrio", ClientType.Company, ClientStatus.Prospect,
            email: "contato@studioequilibrio.com.br", phone: "+55 11 3815-2244", whatsapp: "+55 11 98700-9931",
            doc: "52.310.778/0001-46", company: null, city: "São Caetano do Sul", state: "SP",
            recurring: false, monthly: null, tags: "prospect,wifi",
            notes: "Orçamento de Wi-Fi corporativo enviado — aguardando retorno.");
        var marcelo = NewClient("Marcelo Tavares", ClientType.Individual, ClientStatus.Active,
            email: "marcelo.tavares@gmail.com", phone: null, whatsapp: "+55 11 99182-3360",
            doc: "327.554.198-04", company: null, city: "São Paulo", state: "SP",
            recurring: false, monthly: null, tags: "home-office,formatacao",
            notes: "Atendimento residencial — montagem de home office e backup pessoal.");
        var construtora = NewClient("Construtora Marco Zero", ClientType.Company, ClientStatus.Inactive,
            email: "adm@marcozeroengenharia.com.br", phone: "+55 11 3090-5500", whatsapp: null,
            doc: "12.665.401/0001-88", company: "Marco Zero Engenharia e Construções", city: "Barueri", state: "SP",
            recurring: false, monthly: null, tags: "inativo",
            notes: "Contrato encerrado em 2025 — obra finalizada.");

        var clients = new[] { horizonte, padaria, vidaplena, ramos, veloz, pilates, marcelo, construtora };
        ctx.Clients.AddRange(clients);
        await ctx.SaveChangesAsync(ct);

        // ── Equipamentos ─────────────────────────────────────────────────────────
        ctx.ClientEquipment.AddRange(
            NewEquip(horizonte, "Servidor de Arquivos", "Servidor", "Dell", "PowerEdge T340",
                os: "Windows Server 2022", specs: "Xeon E-2224, 32GB RAM, 2x2TB RAID1", sn: "DLT340-7741"),
            NewEquip(horizonte, "Firewall pfSense", "Rede", "Netgate", "1100",
                os: "pfSense 2.7", specs: "Gateway + VPN site-to-site", sn: "NG1100-2290"),
            NewEquip(padaria, "PDV Caixa 01", "PDV", "Bematech", "RC-8400",
                os: "Linux", specs: "Frente de caixa + impressora térmica", sn: "BEMA-8400-01"),
            NewEquip(vidaplena, "NAS Backup", "Armazenamento", "Synology", "DS920+",
                os: "DSM 7.2", specs: "4x4TB SHR, backup diário criptografado", sn: "SYN-920-5512"),
            NewEquip(veloz, "Estação Fiscal", "Desktop", "Lenovo", "ThinkCentre M70s",
                os: "Windows 11 Pro", specs: "i5-12400, 16GB, SSD 512GB", sn: "LNV-M70-3381"),
            NewEquip(marcelo, "Notebook Pessoal", "Notebook", "Acer", "Aspire 5",
                os: "Windows 11 Home", specs: "Ryzen 5, 16GB, SSD 512GB", sn: "ACR-A5-9920"));

        // ── Chamados ───────────────────────────────────────────────────────────
        int tk = 0;
        Ticket Tk(Client c, string title, string desc, TicketStatus st, TicketPriority pr, TicketCategory cat,
            int createdDaysAgo, decimal? value = null, string? resolution = null) =>
            NewTicket(c, ref tk, title, desc, st, pr, cat, now.AddDays(-createdDaysAgo), value, resolution);

        var tHorizonte = Tk(horizonte, "Servidor de arquivos lento ao abrir planilhas",
            "Usuárias do setor fiscal relatam lentidão ao abrir planilhas compartilhadas no servidor pela manhã.",
            TicketStatus.InProgress, TicketPriority.High, TicketCategory.Infrastructure, 3);
        var tHorizonte2 = Tk(horizonte, "Configurar VPN para home office de 3 colaboradores",
            "Liberar acesso remoto seguro ao servidor de arquivos para 3 contadores em regime híbrido.",
            TicketStatus.Resolved, TicketPriority.Medium, TicketCategory.Network, 18,
            value: 380m, resolution: "VPN WireGuard configurada no pfSense + perfis distribuídos. Testado e validado.");
        var tPadaria = Tk(padaria, "PDV Caixa 02 não emite cupom",
            "Impressora térmica do caixa 2 parou de imprimir após queda de energia.",
            TicketStatus.Resolved, TicketPriority.Critical, TicketCategory.Hardware, 9,
            value: 220m, resolution: "Fonte da impressora substituída e spooler reconfigurado. Operando normal.");
        var tVida = Tk(vidaplena, "Verificar integridade do backup diário",
            "Solicitação de checagem mensal da rotina de backup do prontuário eletrônico (conformidade LGPD).",
            TicketStatus.WaitingClient, TicketPriority.Medium, TicketCategory.Security, 5);
        var tVeloz = Tk(veloz, "Erro intermitente na emissão de NF-e",
            "ERP retorna rejeição 'falha de comunicação SEFAZ' de forma intermitente no período da tarde.",
            TicketStatus.Analyzing, TicketPriority.High, TicketCategory.Software, 2);
        var tRamos = Tk(ramos, "Lentidão no acesso remoto aos autos",
            "Advogados relatam lentidão ao acessar o sistema de gestão jurídica via VPN.",
            TicketStatus.Open, TicketPriority.Medium, TicketCategory.Network, 1);
        var tMarcelo = Tk(marcelo, "Formatação e backup do notebook pessoal",
            "Reinstalação limpa do Windows preservando arquivos pessoais e configuração de backup em nuvem.",
            TicketStatus.Closed, TicketPriority.Low, TicketCategory.Support, 25,
            value: 150m, resolution: "Sistema reinstalado, dados preservados e OneDrive configurado.");
        var tPadaria2 = Tk(padaria, "Atualização do sistema de retaguarda",
            "Aplicar atualização de versão do sistema de gestão e validar relatórios fiscais.",
            TicketStatus.Open, TicketPriority.Low, TicketCategory.Software, 0);

        ctx.Tickets.AddRange(tHorizonte, tHorizonte2, tPadaria, tVida, tVeloz, tRamos, tMarcelo, tPadaria2);
        await ctx.SaveChangesAsync(ct);

        ctx.TicketComments.AddRange(
            NewComment(tHorizonte, "Abertura via portal. Bom dia! A lentidão começou ontem, piora quando todos abrem os arquivos juntos.", fromClient: true, author: PortalContact, daysAgo: 3),
            NewComment(tHorizonte, "Identificado gargalo de I/O no disco do servidor. Vou agendar a migração das planilhas mais pesadas para o SSD.", internalNote: false, author: "Gustavo Almeida", daysAgo: 2),
            NewComment(tHorizonte, "Pico de fragmentação no volume RAID. Avaliar troca por SSD no próximo orçamento.", internalNote: true, author: "Gustavo Almeida", daysAgo: 2),
            NewComment(tVeloz, "Capturado o XML de rejeição. O timeout bate com o horário de backup do ERP — provável concorrência de rede.", internalNote: true, author: "Gustavo Almeida", daysAgo: 1),
            NewComment(tVida, "Backup de ontem íntegro (checksum OK). Aguardando confirmação da clínica para encerrar.", fromClient: false, author: "Gustavo Almeida", daysAgo: 1));
        await ctx.SaveChangesAsync(ct);

        // ── Orçamentos ───────────────────────────────────────────────────────────
        int qn = 0;
        var qHorizonte = NewQuote(horizonte, ref qn, "Upgrade do servidor de arquivos para SSD",
            "Migração dos volumes de dados para SSD corporativo, eliminando o gargalo de I/O do RAID atual.",
            DocumentStatus.Sent, now.AddDays(-2), validDays: 15,
            items: new[]
            {
                ("SSD corporativo 1TB (Datacenter, DWPD alto) — 2 un.", 920m, 2m),
                ("Serviço de migração de dados e reconfiguração do RAID", 600m, 1m),
                ("Janela de manutenção fora do horário comercial", 250m, 1m),
            },
            terms: "Validade de 15 dias. Execução em sábado, fora do horário comercial. Garantia de 90 dias no serviço.");
        var qVeloz = NewQuote(veloz, ref qn, "Estabilização da emissão fiscal (NF-e)",
            "Segmentação de rede e priorização de tráfego fiscal para eliminar as rejeições intermitentes da SEFAZ.",
            DocumentStatus.Accepted, now.AddDays(-12), validDays: 20, acceptedDaysAgo: 6,
            items: new[]
            {
                ("Switch gerenciável 8 portas com VLAN", 480m, 1m),
                ("Configuração de QoS e segmentação de rede", 420m, 1m),
                ("Homologação da emissão fiscal pós-ajuste", 200m, 1m),
            },
            terms: "Validade de 20 dias. Inclui acompanhamento de 7 dias após a implantação.");
        var qPilates = NewQuote(pilates, ref qn, "Projeto de Wi-Fi corporativo",
            "Cobertura Wi-Fi profissional para o studio, com rede separada para alunos e equipe.",
            DocumentStatus.Sent, now.AddDays(-6), validDays: 30,
            items: new[]
            {
                ("Access Point Wi-Fi 6 (teto) — 2 un.", 740m, 2m),
                ("Controladora e configuração de redes (equipe/visitantes)", 380m, 1m),
                ("Cabeamento e instalação", 300m, 1m),
            },
            terms: "Validade de 30 dias. Inclui rede isolada para visitantes e portal de acesso.");
        var qRamos = NewQuote(ramos, ref qn, "Reforço da VPN e do acesso remoto",
            "Substituição da VPN atual por solução WireGuard com perfis individuais e MFA.",
            DocumentStatus.Draft, now.AddDays(-1), validDays: 15,
            items: new[]
            {
                ("Configuração WireGuard com perfis por usuário", 560m, 1m),
                ("Integração de MFA no acesso remoto", 340m, 1m),
            },
            terms: "Rascunho — revisar escopo com o cliente antes do envio.");
        var qMarcelo = NewQuote(marcelo, ref qn, "Montagem de home office",
            "Setup completo de home office com backup pessoal automatizado.",
            DocumentStatus.Rejected, now.AddDays(-30), validDays: 10,
            items: new[]
            {
                ("Organização de cabeamento e setup de 2 monitores", 180m, 1m),
                ("Configuração de backup automático em nuvem", 120m, 1m),
            },
            terms: "Cliente optou por escopo reduzido (apenas o chamado de formatação).");

        ctx.Quotes.AddRange(qHorizonte, qVeloz, qPilates, qRamos, qMarcelo);
        await ctx.SaveChangesAsync(ct);

        // ── Ordens de serviço ────────────────────────────────────────────────────
        int on = 0;
        ctx.ServiceOrders.AddRange(
            NewOrder(horizonte, ref on, "Configuração de VPN para home office", tHorizonte2,
                DocumentStatus.Accepted, completedDaysAgo: 16, labor: 380m, parts: 0m,
                checklist: "✔ pfSense atualizado\n✔ WireGuard habilitado\n✔ 3 perfis gerados e entregues\n✔ Teste de acesso ao servidor OK",
                notes: "Acesso remoto validado com os 3 colaboradores. Latência média 28ms."),
            NewOrder(padaria, ref on, "Reparo do PDV Caixa 02", tPadaria,
                DocumentStatus.Accepted, completedDaysAgo: 8, labor: 120m, parts: 100m,
                checklist: "✔ Fonte da impressora substituída\n✔ Spooler reconfigurado\n✔ Emissão de cupom testada",
                notes: "Recomendado nobreak dedicado para o caixa — orçamento futuro."),
            NewOrder(marcelo, ref on, "Formatação e backup do notebook", tMarcelo,
                DocumentStatus.Accepted, completedDaysAgo: 24, labor: 150m, parts: 0m,
                checklist: "✔ Backup dos dados\n✔ Reinstalação limpa do Windows 11\n✔ Drivers e apps essenciais\n✔ OneDrive configurado",
                notes: "Entregue com backup em nuvem ativo."),
            NewOrder(veloz, ref on, "Implantação de segmentação de rede (NF-e)", tVeloz,
                DocumentStatus.Sent, completedDaysAgo: null, labor: 620m, parts: 480m,
                checklist: "☐ Instalar switch gerenciável\n☐ Criar VLANs\n☐ Aplicar QoS\n☐ Homologar emissão fiscal",
                notes: "Agendado conforme orçamento aprovado.", scheduledDaysFromNow: 3));
        await ctx.SaveChangesAsync(ct);

        // ── Financeiro ───────────────────────────────────────────────────────────
        var tx = new List<Transaction>();
        foreach (var (c, val) in new[] { (horizonte, 890m), (padaria, 450m), (vidaplena, 1200m) })
            for (int m = 4; m >= 0; m--)
                tx.Add(NewTx($"Mensalidade de TI — {c.Name}", TransactionType.Income,
                    TransactionStatus.Paid, val, Cat("Manutenção Recorrente"), c,
                    // mês corrente: pago há 2 dias (entra na "receita do mês"); meses anteriores no dia ~5
                    dueDaysAgo: m == 0 ? 2 : m * 30 - 5, recurring: true));
        tx.Add(NewTx("VPN home office — Horizonte", TransactionType.Income, TransactionStatus.Paid, 380m, Cat("Suporte Técnico"), horizonte, 16));
        tx.Add(NewTx("Reparo PDV — Padaria Estrela", TransactionType.Income, TransactionStatus.Paid, 220m, Cat("Suporte Técnico"), padaria, 8));
        tx.Add(NewTx("Segmentação de rede NF-e — Veloz", TransactionType.Income, TransactionStatus.Pending, 1100m, Cat("Infraestrutura / VPS"), veloz, -3));
        tx.Add(NewTx("Formatação — Marcelo Tavares", TransactionType.Income, TransactionStatus.Paid, 150m, Cat("Suporte Técnico"), marcelo, 24));
        tx.Add(NewTx("Consultoria LGPD — Vida Plena", TransactionType.Income, TransactionStatus.Overdue, 600m, Cat("Consultoria"), vidaplena, 12));
        // Despesas recorrentes pagas no mês corrente (entram na "despesa do mês" → margem realista)
        tx.Add(NewTx("VPS de produção (Hetzner)", TransactionType.Expense, TransactionStatus.Paid, 89m, Cat("Infraestrutura / VPS"), null, 2, recurring: true));
        tx.Add(NewTx("Microsoft 365 Business", TransactionType.Expense, TransactionStatus.Paid, 142m, Cat("Ferramentas e Software"), null, 3, recurring: true));
        // Mês anterior (histórico do gráfico)
        tx.Add(NewTx("VPS de produção (Hetzner)", TransactionType.Expense, TransactionStatus.Paid, 89m, Cat("Infraestrutura / VPS"), null, 32, recurring: true));
        tx.Add(NewTx("Microsoft 365 Business", TransactionType.Expense, TransactionStatus.Paid, 142m, Cat("Ferramentas e Software"), null, 35, recurring: true));
        tx.Add(NewTx("Estoque de peças (fontes/cabos)", TransactionType.Expense, TransactionStatus.Paid, 430m, Cat("Equipamentos"), null, 20));
        tx.Add(NewTx("Assinatura de antivírus corporativo", TransactionType.Expense, TransactionStatus.Pending, 210m, Cat("Ferramentas e Software"), null, -8, recurring: true));
        ctx.Transactions.AddRange(tx);

        // ── Cofre de senhas (AES-256-GCM) ────────────────────────────────────────
        ctx.VaultEntries.AddRange(
            NewVault("Servidor de Arquivos — Horizonte", "Infraestrutura", "administrador", "S3rv!Horizonte#2024", "rdp://10.0.10.5", "Acesso RDP. Porta alterada para 3390.", "servidor,rdp", fav: true),
            NewVault("pfSense — Horizonte", "Rede", "admin", "Pf$enseFw_77!", "https://10.0.10.1", "Firewall + VPN WireGuard.", "firewall,vpn"),
            NewVault("Synology NAS — Vida Plena", "Backup", "backup-admin", "N4s#VidaPlena92", "https://10.0.20.4:5001", "Backup diário do prontuário. 2FA ativo.", "backup,nas", fav: true),
            NewVault("Painel ERP — Auto Peças Veloz", "Sistemas", "ti.veloz", "Erp@Veloz_2024!", "https://erp.autopecasveloz.com.br", "Acesso administrativo ao ERP.", "erp,fiscal"),
            NewVault("Roteador MikroTik — Padaria", "Rede", "admin", "M!krotik_Estrela4", "https://192.168.88.1", "Winbox + Webfig.", "rede,mikrotik"),
            NewVault("Cloudflare — gustavoti.com", "Domínios", "gustavoalm09@gmail.com", "Cf#Dns_Manage21!", "https://dash.cloudflare.com", "DNS e proxy do domínio.", "dns,dominio"),
            NewVault("VPS Produção (Hetzner)", "Infraestrutura", "root", "Htz!Vps_Prod_88x", "ssh://srv.gustavoti.com", "Servidor de produção. Acesso por chave; senha de emergência.", "vps,ssh", fav: true),
            NewVault("Microsoft 365 — Admin", "Sistemas", "admin@gustavoti.com", "M365#Admin_2024!", "https://admin.microsoft.com", "Tenant administrativo.", "m365,email"));

        // ── Servidores ────────────────────────────────────────────────────────────
        ctx.Servers.AddRange(
            NewServer("Produção — Nexus", "srv.gustavoti.com", "10.0.0.2", "Hetzner", ServerType.VPS, ServerStatus.Online,
                os: "Ubuntu 24.04 LTS", location: "Falkenstein, DE", cpu: 4, ram: 8, storage: 160, cost: 89m,
                services: new[] { ("Nexus Web", "443", true), ("PostgreSQL", "5432", false), ("Caddy", "80/443", true) },
                monitoring: true, healthUrl: "https://gustavoti.com"),
            NewServer("NAS Backup — Vida Plena", "nas-vidaplena.local", "10.0.20.4", "Synology", ServerType.HomeServer, ServerStatus.Online,
                os: "DSM 7.2", location: "Santo André, BR", cpu: 4, ram: 4, storage: 16000, cost: null,
                services: new[] { ("Backup diário", "—", false), ("DSM", "5001", false) },
                monitoring: false),
            NewServer("Servidor Arquivos — Horizonte", "fs-horizonte.local", "10.0.10.5", "On-premise", ServerType.Dedicated, ServerStatus.Maintenance,
                os: "Windows Server 2022", location: "São Paulo, BR", cpu: 4, ram: 32, storage: 4000, cost: null,
                services: new[] { ("Compartilhamento SMB", "445", false), ("RDP", "3390", false) },
                monitoring: false));

        // ── Tarefas ──────────────────────────────────────────────────────────────
        ctx.WorkItems.AddRange(
            NewWork("Migrar planilhas do RAID para SSD (Horizonte)", "Após aprovação do orçamento ORC.", WorkItemPriority.High, WorkItemStatus.Todo, "Infra", dueDays: 5, client: horizonte),
            NewWork("Homologar emissão NF-e pós-segmentação (Veloz)", "Validar 10 emissões consecutivas sem rejeição.", WorkItemPriority.Urgent, WorkItemStatus.InProgress, "Fiscal", dueDays: 3, client: veloz),
            NewWork("Revisar rotina de backup (Vida Plena)", "Checagem mensal de integridade — LGPD.", WorkItemPriority.Medium, WorkItemStatus.InProgress, "Backup", dueDays: 2, client: vidaplena),
            NewWork("Enviar orçamento de Wi-Fi (Studio Equilíbrio)", "Follow-up do prospect.", WorkItemPriority.Medium, WorkItemStatus.Done, "Comercial", dueDays: -2, client: pilates),
            NewWork("Comprar estoque de fontes ATX", "Repor estoque de peças de reposição.", WorkItemPriority.Low, WorkItemStatus.Todo, "Compras", dueDays: 10, client: null),
            NewWork("Atualizar firmware dos APs", "Manutenção preventiva trimestral.", WorkItemPriority.Low, WorkItemStatus.Todo, "Manutenção", dueDays: 14, client: null),
            NewWork("Documentar topologia da rede Horizonte", "Diagrama atualizado pós-VPN.", WorkItemPriority.Medium, WorkItemStatus.Done, "Documentação", dueDays: -8, client: horizonte));

        // ── Base de conhecimento ─────────────────────────────────────────────────
        ctx.KnowledgeArticles.AddRange(
            NewArticle("Como configurar VPN WireGuard no pfSense", "wireguard-pfsense",
                "Passo a passo para habilitar o WireGuard, gerar perfis por usuário e liberar o acesso ao servidor de arquivos.",
                "Rede", "vpn,wireguard,pfsense", pinned: true, views: 142),
            NewArticle("Checklist de backup conforme LGPD", "checklist-backup-lgpd",
                "Itens mínimos para validar rotinas de backup em ambientes que tratam dados sensíveis de saúde.",
                "Segurança", "lgpd,backup,compliance", pinned: true, views: 98),
            NewArticle("Resolvendo rejeição intermitente de NF-e", "rejeicao-nfe-sefaz",
                "Diagnóstico de timeouts de comunicação com a SEFAZ causados por concorrência de rede.",
                "Fiscal", "nfe,sefaz,rede", pinned: false, views: 57),
            NewArticle("Padrão de nomenclatura de equipamentos", "padrao-nomenclatura",
                "Convenção interna para nomear estações, servidores e ativos de rede dos clientes.",
                "Processos", "padroes,documentacao", pinned: false, views: 31));

        // ── Notas ────────────────────────────────────────────────────────────────
        ctx.Notes.AddRange(
            NewNote("Senha temporária do switch Veloz", "Trocar a senha padrão do switch novo assim que instalar. Anotar no cofre depois.", "amber", pinned: true),
            NewNote("Ideia: pacote de manutenção preventiva", "Oferecer plano trimestral de manutenção preventiva para os mensalistas (limpeza, firmware, revisão de backup).", "cyan", pinned: false),
            NewNote("Contato fornecedor de peças", "Distribuidor novo com preço melhor em SSD corporativo — pedir cotação na próxima compra.", "green", pinned: false));

        // ── Avaliações ───────────────────────────────────────────────────────────
        ctx.ClientReviews.AddRange(
            NewReview(horizonte, PortalContact, 5, "Resolveram a VPN rapidíssimo e explicaram tudo. Acesso remoto funcionando perfeitamente.", approved: true),
            NewReview(padaria, "Sandra — Padaria Estrela", 5, "Caixa voltou a funcionar no mesmo dia, mesmo com a correria. Atendimento nota 10.", approved: true),
            NewReview(marcelo, "Marcelo Tavares", 5, "Formatou o notebook sem perder nada e ainda configurou o backup. Recomendo!", approved: true),
            NewReview(veloz, "Depto. de TI — Veloz", 4, "Diagnóstico certeiro do problema de NF-e. Aguardando a implantação final.", approved: false));

        // ── Alertas (marcados via RelatedEntityType) ─────────────────────────────
        ctx.Alerts.AddRange(
            NewAlert(AlertType.PaymentDue, AlertSeverity.Warning, "Consultoria LGPD vencida", "Pagamento de R$ 600,00 (Vida Plena) está em atraso."),
            NewAlert(AlertType.ServerDown, AlertSeverity.Info, "Servidor em manutenção", "Servidor de Arquivos — Horizonte está em janela de manutenção."),
            NewAlert(AlertType.ClientWaiting, AlertSeverity.Info, "Chamado aguardando cliente", "Vida Plena — confirmação da checagem de backup pendente."));

        await ctx.SaveChangesAsync(ct);

        // ── Acesso ao portal (isolamento multi-tenant) ───────────────────────────
        await EnsurePortalUserAsync(horizonte.Id);

        return await ComputeStatusAsync(ctx, ct);
    }

    // ───────────────────────────── Helpers ──────────────────────────────────────

    private static Client NewClient(string name, ClientType type, ClientStatus status, string? email, string? phone,
        string? whatsapp, string? doc, string? company, string? city, string? state, bool recurring, decimal? monthly,
        string? tags, string? notes) => new()
    {
        Name = name, Type = type, Status = status, Email = email, Phone = phone, WhatsApp = whatsapp,
        Document = doc, CompanyName = company, City = city, State = state, IsRecurring = recurring,
        MonthlyValue = monthly, Tags = tags, Notes = notes, CreatedBy = Marker,
    };

    private static ClientEquipment NewEquip(Client c, string name, string type, string brand, string model,
        string os, string specs, string sn) => new()
    {
        ClientId = c.Id, Name = name, Type = type, Brand = brand, Model = model, OperatingSystem = os,
        Specs = specs, SerialNumber = sn, IsActive = true, CreatedBy = Marker,
    };

    private static Ticket NewTicket(Client c, ref int seq, string title, string desc, TicketStatus st,
        TicketPriority pr, TicketCategory cat, DateTime createdAt, decimal? value, string? resolution)
    {
        seq++;
        var resolved = st is TicketStatus.Resolved or TicketStatus.Closed;
        return new Ticket
        {
            Number = $"NX-{createdAt:yyMMdd}-{seq:D4}",
            Title = title, Description = desc, ClientId = c.Id,
            ContactName = c.Name, ContactEmail = c.Email, ContactWhatsApp = c.WhatsApp,
            Status = st, Priority = pr, Category = cat,
            CreatedAt = createdAt, CreatedBy = Marker,
            StartedAt = st >= TicketStatus.InProgress ? createdAt.AddHours(4) : null,
            ResolvedAt = resolved ? createdAt.AddDays(1) : null,
            ClosedAt = st == TicketStatus.Closed ? createdAt.AddDays(2) : null,
            Resolution = resolution, ServiceValue = value,
            SlaDeadline = createdAt.AddDays(pr == TicketPriority.Critical ? 1 : 3),
        };
    }

    private static TicketComment NewComment(Ticket t, string content, int daysAgo, string author,
        bool fromClient = false, bool internalNote = false) => new()
    {
        TicketId = t.Id, Content = content, IsFromClient = fromClient, IsInternal = internalNote,
        AuthorName = author, CreatedAt = DateTime.UtcNow.AddDays(-daysAgo), CreatedBy = Marker,
    };

    private static Quote NewQuote(Client c, ref int seq, string title, string desc, DocumentStatus status,
        DateTime createdAt, int validDays, (string Desc, decimal Price, decimal Qty)[] items, string terms,
        int? acceptedDaysAgo = null)
    {
        seq++;
        var q = new Quote
        {
            Number = $"ORC-{createdAt:yyMMdd}-{seq:D3}",
            Title = title, Description = desc, ClientId = c.Id, ClientName = c.Name,
            Status = status, ValidUntil = createdAt.AddDays(validDays), Terms = terms,
            CreatedAt = createdAt, CreatedBy = Marker,
            SentAt = status >= DocumentStatus.Sent ? createdAt.AddHours(2) : null,
            AcceptedAt = acceptedDaysAgo is int d ? DateTime.UtcNow.AddDays(-d) : null,
        };
        int order = 0;
        foreach (var it in items)
            q.Items.Add(new QuoteItem
            {
                Description = it.Desc, UnitPrice = it.Price, Quantity = it.Qty,
                Total = it.Price * it.Qty, DisplayOrder = order++,
            });
        q.Subtotal = q.Items.Sum(i => i.Total);
        q.Total = q.Subtotal - q.Discount + q.Tax;
        return q;
    }

    private static ServiceOrder NewOrder(Client c, ref int seq, string title, Ticket? ticket, DocumentStatus status,
        int? completedDaysAgo, decimal labor, decimal parts, string checklist, string notes,
        int? scheduledDaysFromNow = null)
    {
        seq++;
        var created = DateTime.UtcNow.AddDays(-(completedDaysAgo ?? 0) - 1);
        return new ServiceOrder
        {
            Number = $"OS-{created:yyMMdd}-{seq:D3}",
            Title = title, ClientId = c.Id, TicketId = ticket?.Id, Status = status,
            TechnicianNotes = notes, Checklist = checklist,
            LaborValue = labor, PartsValue = parts, Total = labor + parts,
            CreatedAt = created, CreatedBy = Marker,
            ScheduledDate = scheduledDaysFromNow is int sd ? DateTime.UtcNow.AddDays(sd) : null,
            StartedAt = completedDaysAgo is not null ? created.AddHours(2) : null,
            CompletedAt = completedDaysAgo is int cd ? DateTime.UtcNow.AddDays(-cd) : null,
        };
    }

    private static Transaction NewTx(string title, TransactionType type, TransactionStatus status, decimal amount,
        Guid? categoryId, Client? client, int dueDaysAgo, bool recurring = false) => new()
    {
        Title = title, Type = type, Status = status, Amount = amount, CategoryId = categoryId,
        ClientId = client?.Id, DueDate = DateTime.UtcNow.AddDays(-dueDaysAgo),
        PaidAt = status == TransactionStatus.Paid ? DateTime.UtcNow.AddDays(-dueDaysAgo) : null,
        PaymentMethod = status == TransactionStatus.Paid ? "PIX" : null,
        IsRecurring = recurring, RecurrenceType = recurring ? RecurrenceType.Monthly : RecurrenceType.None,
        CreatedAt = DateTime.UtcNow.AddDays(-dueDaysAgo - 1), CreatedBy = Marker,
    };

    private VaultEntry NewVault(string name, string category, string user, string password, string url,
        string notes, string tags, bool fav = false) => new()
    {
        Name = name, Category = category, Username = user,
        EncryptedPassword = enc.Encrypt(password),
        EncryptedNotes = string.IsNullOrEmpty(notes) ? null : enc.Encrypt(notes),
        Url = url, Tags = tags, IsFavorite = fav,
        PasswordChangedAt = DateTime.UtcNow.AddDays(-30), CreatedBy = Marker,
    };

    private static ServerEntry NewServer(string name, string hostname, string ip, string provider, ServerType type,
        ServerStatus status, string os, string location, int cpu, int ram, int storage, decimal? cost,
        (string Name, string Port, bool Public)[] services, bool monitoring = true, string? healthUrl = null)
    {
        var srv = new ServerEntry
        {
            Name = name, Hostname = hostname, IpAddress = ip, Provider = provider, Type = type, Status = status,
            OperatingSystem = os, Location = location, CpuCores = cpu, RamGb = ram, StorageGb = storage,
            MonthlyCost = cost, MonitoringEnabled = monitoring, HealthCheckUrl = healthUrl,
            LastCheckedAt = DateTime.UtcNow.AddMinutes(-5), CreatedBy = Marker,
        };
        foreach (var s in services)
            srv.Services.Add(new ServerService { Name = s.Name, Port = s.Port, IsPublic = s.Public, IsRunning = status == ServerStatus.Online });
        return srv;
    }

    private static WorkItem NewWork(string title, string desc, WorkItemPriority pr, WorkItemStatus st,
        string category, int dueDays, Client? client) => new()
    {
        Title = title, Description = desc, Priority = pr, Status = st, Category = category,
        DueDate = DateTime.UtcNow.AddDays(dueDays), RelatedClientId = client?.Id,
        CompletedAt = st == WorkItemStatus.Done ? DateTime.UtcNow.AddDays(dueDays) : null,
        CreatedBy = Marker,
    };

    private static KnowledgeArticle NewArticle(string title, string slug, string summary, string category,
        string tags, bool pinned, int views) => new()
    {
        Title = title, Slug = slug, Summary = summary, Category = category, Tags = tags,
        Content = summary + "\n\n(Conteúdo completo do artigo.)", IsPinned = pinned, IsPublic = false,
        Views = views, CreatedBy = Marker,
    };

    private static Note NewNote(string title, string content, string color, bool pinned) => new()
    {
        Title = title, Content = content, Color = color, IsPinned = pinned, CreatedBy = Marker,
    };

    private static ClientReview NewReview(Client c, string reviewer, int rating, string comment, bool approved) => new()
    {
        ClientId = c.Id, ReviewerName = reviewer, Rating = rating, Comment = comment,
        IsApproved = approved, IsPublic = true, CreatedBy = Marker,
    };

    private static Alert NewAlert(AlertType type, AlertSeverity sev, string title, string message) => new()
    {
        Type = type, Severity = sev, Title = title, Message = message, IsRead = false,
        RelatedEntityType = Marker, // marcador p/ purge (Alert é BaseEntity, sem CreatedBy)
    };

    private async Task EnsurePortalUserAsync(Guid clientId)
    {
        var password = config["Seed:PortalPassword"];
        if (string.IsNullOrWhiteSpace(password)) password = DefaultPortalPassword;

        var existing = await users.FindByEmailAsync(PortalEmail);
        if (existing is not null)
        {
            existing.ClientId = clientId;
            await users.UpdateAsync(existing);
            if (!await users.IsInRoleAsync(existing, "Client"))
                await users.AddToRoleAsync(existing, "Client");
            return;
        }

        var user = new ApplicationUser
        {
            UserName = PortalEmail, Email = PortalEmail, EmailConfirmed = true,
            FullName = PortalContact, ClientId = clientId, MustChangePassword = false, IsActive = true,
        };
        if (await users.CreateAsync(user, password) is { Succeeded: true })
            await users.AddToRoleAsync(user, "Client");
    }
}
