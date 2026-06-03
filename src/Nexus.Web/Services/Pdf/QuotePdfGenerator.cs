using System.Globalization;
using Nexus.Application.Features.Quotes;
using Nexus.Domain.Enums;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

namespace Nexus.Web.Services.Pdf;

public class QuotePdfGenerator
{
    private static readonly CultureInfo Br = CultureInfo.GetCultureInfo("pt-BR");

    public byte[] Render(QuoteDetailDto q)
    {
        return Document.Create(doc =>
        {
            doc.Page(p =>
            {
                p.Size(PageSizes.A4);
                p.Margin(36);
                p.PageColor(BrandedPdf.BgDeep);
                p.DefaultTextStyle(t => t.FontFamily(BrandedPdf.FontUI).FontSize(10).FontColor(BrandedPdf.Text1));

                p.Header().Element(c => Header(c, q));
                p.Content().PaddingVertical(12).Element(c => Body(c, q));
                p.Footer().Element(Footer);
            });
        }).GeneratePdf();
    }

    private static void Header(QuestPDF.Infrastructure.IContainer c, QuoteDetailDto q)
    {
        c.Row(row =>
        {
            row.RelativeItem().Column(col =>
            {
                col.Item().Text(BrandedPdf.OwnerName)
                    .FontFamily(BrandedPdf.FontDisplay).FontSize(20).Bold().FontColor(BrandedPdf.Text1)
                    .LetterSpacing(0.12f);
                col.Item().PaddingTop(2).Text(BrandedPdf.OwnerSub)
                    .FontSize(9).FontColor(BrandedPdf.Brand).LetterSpacing(0.12f);
            });
            row.ConstantItem(140).AlignRight().Column(col =>
            {
                col.Item().Text("ORÇAMENTO").FontSize(9).LetterSpacing(0.1f).FontColor(BrandedPdf.Text3);
                var badge = StatusBadge(q.Status);
                col.Item().PaddingTop(3).Text(t =>
                {
                    t.Span("●  ").FontColor(badge.Dot).FontSize(8);
                    t.Span(badge.Label).FontSize(8).FontColor(BrandedPdf.Text2);
                });
            });
        });
    }

    private static (string Label, Color Dot) StatusBadge(DocumentStatus s) => s switch
    {
        DocumentStatus.Sent => ("Enviado", BrandedPdf.Brand),
        DocumentStatus.Accepted => ("Aceito", Colors.Green.Medium),
        DocumentStatus.Rejected => ("Recusado", Colors.Red.Medium),
        DocumentStatus.Expired => ("Expirado", BrandedPdf.Text3),
        _ => ("Rascunho", BrandedPdf.Text3),
    };

    private static void Body(QuestPDF.Infrastructure.IContainer c, QuoteDetailDto q)
    {
        c.Column(col =>
        {
            // Hairline ciano
            col.Item().PaddingVertical(8).LineHorizontal(1).LineColor(BrandedPdf.Brand);

            // Bloco número + cliente
            col.Item().PaddingVertical(8).Row(row =>
            {
                row.RelativeItem().Column(c2 =>
                {
                    c2.Item().Text("Número").FontSize(8).FontColor(BrandedPdf.Text3).LetterSpacing(0.05f);
                    c2.Item().Text(q.Number).FontSize(13).Bold().FontFamily(BrandedPdf.FontMono).FontColor(BrandedPdf.Text1);
                });
                row.RelativeItem(2).Column(c2 =>
                {
                    c2.Item().Text("Cliente").FontSize(8).FontColor(BrandedPdf.Text3).LetterSpacing(0.05f);
                    c2.Item().Text(string.IsNullOrWhiteSpace(q.ClientName) ? "—" : q.ClientName!)
                        .FontSize(13).Bold().FontColor(BrandedPdf.Text1);
                });
                row.ConstantItem(110).AlignRight().Column(c2 =>
                {
                    c2.Item().Text("Emissão").FontSize(8).FontColor(BrandedPdf.Text3).LetterSpacing(0.05f);
                    c2.Item().Text(q.CreatedAt.ToString("dd/MM/yyyy")).FontSize(13).FontColor(BrandedPdf.Text1);
                });
            });

            col.Item().Text(q.Title).FontSize(15).Bold().FontColor(BrandedPdf.Text1);
            if (!string.IsNullOrWhiteSpace(q.Description))
                col.Item().PaddingTop(4).Text(q.Description!).FontSize(10).FontColor(BrandedPdf.Text2);

            col.Item().PaddingTop(14).Element(c2 => BuildItemsTable(c2, q));

            col.Item().PaddingTop(16).Row(row =>
            {
                row.RelativeItem();
                row.ConstantItem(220).Column(t =>
                {
                    Totals(t, "Subtotal", q.Subtotal, false);
                    if (q.Discount > 0) Totals(t, "Desconto", -q.Discount, false);
                    if (q.Tax > 0) Totals(t, "Impostos", q.Tax, false);
                    t.Item().PaddingVertical(4).LineHorizontal(1).LineColor(BrandedPdf.Border);
                    Totals(t, "TOTAL", q.Total, true);
                });
            });

            if (!string.IsNullOrWhiteSpace(q.Notes))
            {
                col.Item().PaddingTop(20).Element(box =>
                {
                    box.Padding(12).Background(BrandedPdf.BgSurface).Border(1).BorderColor(BrandedPdf.Border)
                        .Column(t =>
                        {
                            t.Item().Text("OBSERVAÇÕES").FontSize(8).LetterSpacing(0.1f).FontColor(BrandedPdf.Brand);
                            t.Item().PaddingTop(4).Text(q.Notes!).FontSize(10).FontColor(BrandedPdf.Text2);
                        });
                });
            }

            if (!string.IsNullOrWhiteSpace(q.Terms))
            {
                col.Item().PaddingTop(12).Element(box =>
                {
                    box.Padding(12).Background(BrandedPdf.BgSurface).Border(1).BorderColor(BrandedPdf.Border)
                        .Column(t =>
                        {
                            t.Item().Text("CONDIÇÕES").FontSize(8).LetterSpacing(0.1f).FontColor(BrandedPdf.Brand);
                            t.Item().PaddingTop(4).Text(q.Terms!).FontSize(9).FontColor(BrandedPdf.Text2);
                        });
                });
            }

            if (q.ValidUntil is DateTime v)
            {
                col.Item().PaddingTop(16).AlignCenter().Text(t =>
                {
                    t.Span("Válido até  ").FontSize(9).FontColor(BrandedPdf.Text3);
                    t.Span(v.ToString("dd/MM/yyyy")).FontSize(9).FontColor(BrandedPdf.Brand).Bold();
                });
            }
        });
    }

    private static void BuildItemsTable(QuestPDF.Infrastructure.IContainer c, QuoteDetailDto q)
    {
        c.Table(t =>
        {
            t.ColumnsDefinition(cd =>
            {
                cd.RelativeColumn(5);
                cd.ConstantColumn(60);
                cd.ConstantColumn(80);
                cd.ConstantColumn(80);
            });

            t.Header(h =>
            {
                h.Cell().PaddingBottom(6).Text("Descrição").FontSize(8).LetterSpacing(0.05f).FontColor(BrandedPdf.Brand);
                h.Cell().AlignCenter().PaddingBottom(6).Text("Qtd").FontSize(8).LetterSpacing(0.05f).FontColor(BrandedPdf.Brand);
                h.Cell().AlignRight().PaddingBottom(6).Text("Unitário").FontSize(8).LetterSpacing(0.05f).FontColor(BrandedPdf.Brand);
                h.Cell().AlignRight().PaddingBottom(6).Text("Total").FontSize(8).LetterSpacing(0.05f).FontColor(BrandedPdf.Brand);

                h.Cell().ColumnSpan(4).BorderBottom(1).BorderColor(BrandedPdf.Border);
            });

            foreach (var i in q.Items)
            {
                t.Cell().PaddingVertical(6).Text(i.Description).FontSize(10).FontColor(BrandedPdf.Text1);
                t.Cell().AlignCenter().PaddingVertical(6).Text(i.Quantity.ToString("0.##", Br)).FontSize(10).FontColor(BrandedPdf.Text2);
                t.Cell().AlignRight().PaddingVertical(6).Text(i.UnitPrice.ToString("C2", Br)).FontSize(10).FontColor(BrandedPdf.Text2);
                t.Cell().AlignRight().PaddingVertical(6).Text(i.Total.ToString("C2", Br)).FontSize(10).Bold().FontColor(BrandedPdf.Text1);

                t.Cell().ColumnSpan(4).BorderBottom(1).BorderColor(BrandedPdf.Border);
            }
        });
    }

    private static void Totals(QuestPDF.Fluent.ColumnDescriptor col, string label, decimal value, bool emphasized)
    {
        col.Item().Row(r =>
        {
            r.RelativeItem().Text(label)
                .FontSize(emphasized ? 11 : 10)
                .FontColor(emphasized ? BrandedPdf.Brand : BrandedPdf.Text2);
            r.ConstantItem(110).AlignRight().Text(value.ToString("C2", Br))
                .FontSize(emphasized ? 14 : 11)
                .Bold()
                .FontColor(emphasized ? BrandedPdf.Brand : BrandedPdf.Text1);
        });
    }

    private static void Footer(QuestPDF.Infrastructure.IContainer c)
    {
        c.Column(col =>
        {
            col.Item().LineHorizontal(0.5f).LineColor(BrandedPdf.Border);
            col.Item().PaddingTop(8).Row(row =>
            {
                row.RelativeItem().Text(t =>
                {
                    t.Span(BrandedPdf.OwnerWhatsApp + "  ·  ").FontSize(8).FontColor(BrandedPdf.Text2);
                    t.Span(BrandedPdf.OwnerEmail).FontSize(8).FontColor(BrandedPdf.Text2);
                });
                row.ConstantItem(120).AlignRight().Text(BrandedPdf.OwnerSite)
                    .FontSize(8).FontColor(BrandedPdf.Brand).Bold();
            });
        });
    }

    public static string FileName(QuoteDetailDto q) => $"orcamento-{q.Number}.pdf";
}
