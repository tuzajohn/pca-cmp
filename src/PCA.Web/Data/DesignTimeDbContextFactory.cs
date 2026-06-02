using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace PCA.Web.Data;

/// <summary>
/// Used exclusively by EF Core tooling (dotnet ef migrations add/remove).
/// This avoids needing a live MySQL connection when running design-time commands.
/// </summary>
public class DesignTimeDbContextFactory : IDesignTimeDbContextFactory<ApplicationDbContext>
{
    public ApplicationDbContext CreateDbContext(string[] args)
    {
        var configuration = new ConfigurationBuilder()
            .SetBasePath(Directory.GetCurrentDirectory())
            .AddJsonFile("appsettings.json", optional: false)
            .AddJsonFile("appsettings.Development.json", optional: true)
            .AddEnvironmentVariables()
            .Build();

        var connectionString = configuration.GetConnectionString("DefaultConnection")
            ?? throw new InvalidOperationException("Connection string 'DefaultConnection' not found.");

        // Use a hardcoded server version for design-time so no live connection is needed.
        // This must match or be lower than your actual MySQL server version.
        // Override via MYSQL_VERSION env var if needed: e.g. "8.4.0-mysql"
        var versionString = configuration["MySqlVersion"] ?? "8.0.33-mysql";
        var serverVersion = ServerVersion.Parse(versionString);

        var optionsBuilder = new DbContextOptionsBuilder<ApplicationDbContext>();
        optionsBuilder.UseMySql(connectionString, serverVersion);

        return new ApplicationDbContext(optionsBuilder.Options);
    }
}
