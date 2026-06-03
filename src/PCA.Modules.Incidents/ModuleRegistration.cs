using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using PCA.Modules.Incidents.Services;
using PCA.Shared;

namespace PCA.Modules.Incidents;

public class ModuleRegistration : IModuleRegistration
{
    public void Register(IServiceCollection services, IConfiguration configuration)
    {
        services.AddScoped<IIncidentService, IncidentService>();
    }
}
