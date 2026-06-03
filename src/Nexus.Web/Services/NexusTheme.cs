using MudBlazor;

namespace Nexus.Web.Services;

/// <summary>
/// Tema do MudBlazor alinhado com a Foundation v4 (Resend/Linear style).
/// Mantém o ciano #00c2ff como acento mas adota a paleta cinza-neutro do CSS
/// (#0a0a0a/#111/#161616/etc), pra que dialogs, snackbars, popovers, tabelas
/// e form fields fiquem coerentes com `.lp4-*` e o admin shell.
/// </summary>
public static class NexusTheme
{
    public static readonly MudTheme Theme = new()
    {
        PaletteDark = new PaletteDark
        {
            // Brand
            Primary = "#00c2ff",
            PrimaryContrastText = "#0a0a0a",
            Secondary = "#38d3ff",
            Tertiary = "#58d8ff",

            // Backgrounds — cinza neutro escalonado (foundation v4)
            Background = "#0a0a0a",
            BackgroundGray = "#0a0a0a",
            Surface = "#111111",
            DrawerBackground = "#0a0a0a",
            DrawerText = "#a1a1aa",
            DrawerIcon = "#a1a1aa",
            AppbarBackground = "rgba(10,10,10,0.72)",
            AppbarText = "#fafafa",

            // Texto
            TextPrimary = "#fafafa",
            TextSecondary = "#a1a1aa",
            TextDisabled = "#52525b",

            // Ações
            ActionDefault = "#a1a1aa",
            ActionDisabled = "#3f3f46",
            ActionDisabledBackground = "#161616",

            // Linhas/dividers
            Divider = "rgba(255,255,255,0.08)",
            DividerLight = "rgba(255,255,255,0.05)",
            LinesDefault = "rgba(255,255,255,0.08)",
            LinesInputs = "rgba(255,255,255,0.14)",
            TableLines = "rgba(255,255,255,0.06)",
            TableStriped = "rgba(255,255,255,0.02)",
            TableHover = "rgba(255,255,255,0.04)",

            // Estados (alinhados com `--nx-success/warning/error` do CSS)
            Success = "#22c55e",
            Info = "#00c2ff",
            Warning = "#f59e0b",
            Error = "#ef4444",
            Dark = "#111111",
            OverlayDark = "rgba(10,10,10,0.78)"
        },
        Typography = new Typography
        {
            Default = new DefaultTypography
            {
                FontFamily = ["Inter", "system-ui", "sans-serif"],
                FontSize = "0.875rem",
                LetterSpacing = "-0.005em"
            },
            H1 = new H1Typography { FontFamily = ["Inter", "sans-serif"], FontWeight = "700", FontSize = "2.25rem", LetterSpacing = "-0.025em" },
            H2 = new H2Typography { FontFamily = ["Inter", "sans-serif"], FontWeight = "700", FontSize = "1.875rem", LetterSpacing = "-0.02em" },
            H3 = new H3Typography { FontFamily = ["Inter", "sans-serif"], FontWeight = "600", FontSize = "1.5rem", LetterSpacing = "-0.015em" },
            H4 = new H4Typography { FontFamily = ["Inter", "sans-serif"], FontWeight = "600", FontSize = "1.25rem", LetterSpacing = "-0.01em" },
            H5 = new H5Typography { FontFamily = ["Inter", "sans-serif"], FontWeight = "600", FontSize = "1.125rem" },
            H6 = new H6Typography { FontFamily = ["Inter", "sans-serif"], FontWeight = "600", FontSize = "1rem" },
            Button = new ButtonTypography
            {
                FontFamily = ["Inter", "sans-serif"],
                FontWeight = "600",
                TextTransform = "none",
                LetterSpacing = "-0.005em"
            },
            Caption = new CaptionTypography { FontFamily = ["Inter", "sans-serif"], FontSize = "0.75rem", FontWeight = "400" }
        },
        LayoutProperties = new LayoutProperties
        {
            DefaultBorderRadius = "10px",
            DrawerWidthLeft = "260px",
            AppbarHeight = "60px"
        },
        ZIndex = new ZIndex
        {
            // Sino fica acima de drawer/appbar
            Popover = 1500,
            Snackbar = 1600
        }
    };
}
