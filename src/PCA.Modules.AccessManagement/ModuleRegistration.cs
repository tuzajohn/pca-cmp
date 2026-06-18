using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using PCA.Modules.AccessManagement.Services;
using PCA.Shared;

namespace PCA.Modules.AccessManagement;

public class ModuleRegistration : IModuleRegistration
{
    public void Register(IServiceCollection services, IConfiguration configuration)
    {
        services.AddScoped<IAccessManagementService, AccessManagementService>();
    }
}
