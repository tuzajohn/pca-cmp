namespace PCA.Web.Models;

public class ThemeSettings
{
    public int Id { get; set; } = 1;
    public string PrimaryColor { get; set; } = "#e8533a";
    public string SidebarBg { get; set; } = "#ffffff";
    public string SidebarText { get; set; } = "#374151";
    public string AppBg { get; set; } = "#f0f4ed";
    public string CardBg { get; set; } = "#ffffff";
    public string TopbarBg { get; set; } = "#ffffff";
    public string AccentHover { get; set; } = "#c73f28";
    public string FontFamily { get; set; } = "Inter, system-ui, sans-serif";
    public string LogoText { get; set; } = "PCA";
}
