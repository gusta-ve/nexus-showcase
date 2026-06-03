using System.Globalization;
using Nexus.Application.Features.ServiceOrders;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

namespace Nexus.Web.Services.Pdf;

public class ServiceOrderPdfGenerator
{
    private static readonly CultureInfo Br = CultureInfo.GetCultureInfo("pt-BR");

    public byte[] Render(ServiceOrderDetailDto o)
    {
        return Document.Create(doc =>
        {
            doc.Page(p =>
            {
                p.Size(PageSizes.A4);
                p.Margin(36);
                p.PageColor(BrandedPdf.BgDeep);
                p.DefaultTextStyle(t => t.FontFamily(BrandedPdf.FontUI).FontSize(10).FontColor(BrandedPdf.Text1));

                p.Header().Element(Header);
                p.Content().PaddingVertical(12).Element(c => Body(c, o));
                p.Footer().Element(Footer);
            });
        }).GeneratePdf();
    }

    private static void Header(QuestPDF.Infrastructure.IContainer c)
    {
        c.Row(row =>
        {
            row.RelativeItem().Column(col =>
            {
                col.Item().Text(BrandedPdf.OwnerName)
                    .FontFamily(BrandedPdf.FontDisplay).FontSize(20).Bold().FontColor(BrandedPdf.Text1).LetterSpacing(0.12f);
                col.Item().PaddingTop(2).Text(BrandedPdf.OwnerSub)
                    .FontSize(9).FontColor(BrandedPdf.Brand).LetterSpacing(0.12f);
            });
            row.ConstantItem(180).AlignRight().Column(col =>
            {
                col.Item().Text("ORDEM DE SERVIÇO").FontSize(9).LetterSpacing(0.1f).FontColor(BrandedPdf.Text3);
            });
        });
    }

    private static void Body(QuestPDF.Infrastructure.IContainer c, ServiceOrderDetailDto o)
    {
        c.Column(col =>
        {
            col.Item().PaddingVertical(8).LineHorizontal(1).LineColor(BrandedPdf.Brand);

            col.Item().PaddingVertical(8).Row(row =>
            {
                row.RelativeItem().Column(c2 =>
                {
                    c2.Item().Text("Número").FontSize(8).FontColor(BrandedPdf.Text3);
                    c2.Item().Text(o.Number).FontSize(13).Bold().FontFamily(BrandedPdf.FontMono).FontColor(BrandedPdf.Text1);
                });
                row.RelativeItem(2).Column(c2 =>
                {
                    c2.Item().Text("Cliente").FontSize(8).FontColor(BrandedPdf.Text3);
                    c2.Item().Text(string.IsNullOrWhiteSpace(o.ClientName) ? "—" : o.ClientName!)
                        .FontSize(13).Bold().FontColor(BrandedPdf.Text1);
                });
                row.ConstantItem(110).AlignRight().Column(c2 =>
                {
                    c2.Item().Text("Emissão").FontSize(8).FontColor(BrandedPdf.Text3);
                    c2.Item().Text(o.CreatedAt.ToString("dd/MM/yyyy")).FontSize(13).FontColor(BrandedPdf.Text1);
                });
            });

            col.Item().Text(o.Title).FontSize(15).Bold().FontColor(BrandedPdf.Text1);
            if (!string.IsNullOrWhiteSpace(o.Description))
                col.Item().PaddingTop(4).Text(o.Description!).FontSize(10).FontColor(BrandedPdf.Text2);

            if (!string.IsNullOrWhiteSpace(o.Checklist))
            {
                col.Item().PaddingTop(14).Element(box =>
                {
                    box.Padding(12).Background(BrandedPdf.BgSurface).Border(1).BorderColor(BrandedPdf.Border)
                        .Column(t =>
                        {
                            t.Item().Text("CHECKLIST EXECUTADO").FontSize(8).FontColor(BrandedPdf.Brand);
                            t.Item().PaddingTop(6).Text(o.Checklist!).FontSize(10).FontColor(BrandedPdf.Text1);
                        });
                });
            }

            if (!string.IsNullOrWhiteSpace(o.TechnicianNotes))
            {
                col.Item().PaddingTop(12).Element(box =>
                {
                    box.Padding(12).Background(BrandedPdf.BgSurface).Border(1).BorderColor(BrandedPdf.Border)
                        .Column(t =>
                        {
                            t.Item().Text("OBSERVAÇÕES TÉCNICAS").FontSize(8).FontColor(BrandedPdf.Brand);
                            t.Item().PaddingTop(6).Text(o.TechnicianNotes!).FontSize(10).FontColor(BrandedPdf.Text2);
                        });
                });
            }

            col.Item().PaddingTop(16).Row(row =>
            {
                row.RelativeItem();
                row.ConstantItem(220).Column(t =>
                {
                    if ((o.LaborValue ?? 0) > 0) Totals(t, "Mão de obra", o.LaborValue!.Value, false);
                    if ((o.PartsValue ?? 0) > 0) Totals(t, "Peças/materiais", o.PartsValue!.Value, false);
                    if ((o.Discount ?? 0) > 0) Totals(t, "Desconto", -o.Discount!.Value, false);
                    if ((o.Total ?? 0) > 0)
                    {
                        t.Item().PaddingVertical(4).LineHorizontal(1).LineColor(BrandedPdf.Border);
                        Totals(t, "TOTAL", o.Total!.Value, true);
                    }
                });
            });

            col.Item().PaddingTop(40).Row(row =>
            {
                row.RelativeItem().Column(s =>
                {
                    s.Item().LineHorizontal(0.5f).LineColor(BrandedPdf.Text3);
                    s.Item().PaddingTop(4).AlignCenter().Text("Assinatura do cliente").FontSize(9).FontColor(BrandedPdf.Text3);
                });
                row.ConstantItem(30);
                row.RelativeItem().Column(s =>
                {
                    s.Item().LineHorizontal(0.5f).LineColor(BrandedPdf.Text3);
                    s.Item().PaddingTop(4).AlignCenter().Text("Técnico responsável").FontSize(9).FontColor(BrandedPdf.Text3);
                });
            });
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

    public static string FileName(ServiceOrderDetailDto o) => $"os-{o.Number}.pdf";
}
