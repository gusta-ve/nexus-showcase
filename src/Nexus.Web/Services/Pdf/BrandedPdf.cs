using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

namespace Nexus.Web.Services.Pdf;

/// <summary>
/// Tokens visuais do brand "Gustavo Almeida — Suporte TI · Infra" — replicam
/// no PDF a mesma paleta/tipografia da landing pra unidade visual.
/// </summary>
public static class BrandedPdf
{
    // Cores (preto profundo + ciano elétrico, como o cartão)
    public static readonly Color BgDeep = Color.FromHex("#050608");
    public static readonly Color BgSurface = Color.FromHex("#0d1115");
    public static readonly Color Border = Color.FromHex("#1f2a33");
    public static readonly Color Brand = Color.FromHex("#00c2ff");
    public static readonly Color BrandDeep = Color.FromHex("#0096c9");
    public static readonly Color Text1 = Colors.White;
    public static readonly Color Text2 = Color.FromHex("#93a1ad");
    public static readonly Color Text3 = Color.FromHex("#5a6a75");

    public const string FontDisplay = "Helvetica"; // serve como fallback condensed
    public const string FontUI = "Helvetica";
    public const string FontMono = "Courier";

    public const string OwnerName = "GUSTAVO ALMEIDA";
    public const string OwnerSub = "SUPORTE TI · INFRA";
    public const string OwnerWhatsApp = "+55 11 94265-3054";
    public const string OwnerEmail = "gustavoalm1@live.com";
    public const string OwnerSite = "gustavoti.com";
}
