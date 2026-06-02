using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using PCA.Modules.ChangeManagement.Services;
using PCA.Shared;

namespace PCA.Modules.ChangeManagement;

public class ModuleRegistration : IModuleRegistration
{
    public void Register(IServiceCollection services, IConfiguration configuration)
    {
        services.AddScoped<IChangeRequestService, ChangeRequestService>();
    }
}
