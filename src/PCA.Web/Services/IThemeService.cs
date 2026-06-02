using PCA.Web.Models;

namespace PCA.Web.Services;

public interface IThemeService
{
    Task<ThemeSettings> GetThemeAsync();
    Task SaveThemeAsync(ThemeSettings settings);
}
