using Microsoft.EntityFrameworkCore;
using PCA.Web.Data;
using PCA.Web.Models;

namespace PCA.Web.Services;

public class ThemeService : IThemeService
{
    private readonly ApplicationDbContext _db;

    public ThemeService(ApplicationDbContext db)
    {
        _db = db;
    }

    public async Task<ThemeSettings> GetThemeAsync()
    {
        var theme = await _db.ThemeSettings.FindAsync(1);
        if (theme == null)
        {
            theme = new ThemeSettings { Id = 1 };
            _db.ThemeSettings.Add(theme);
            await _db.SaveChangesAsync();
        }
        return theme;
    }

    public async Task SaveThemeAsync(ThemeSettings settings)
    {
        settings.Id = 1;
        var existing = await _db.ThemeSettings.FindAsync(1);
        if (existing == null)
        {
            _db.ThemeSettings.Add(settings);
        }
        else
        {
            existing.PrimaryColor = settings.PrimaryColor;
            existing.SidebarBg = settings.SidebarBg;
            existing.SidebarText = settings.SidebarText;
            existing.AppBg = settings.AppBg;
            existing.CardBg = settings.CardBg;
            existing.TopbarBg = settings.TopbarBg;
            existing.AccentHover = settings.AccentHover;
            existing.FontFamily = settings.FontFamily;
            existing.LogoText = settings.LogoText;
        }
        await _db.SaveChangesAsync();
    }
}
