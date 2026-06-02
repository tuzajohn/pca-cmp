using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using PCA.Shared;

namespace PCA.Modules.Identity;

public class ModuleRegistration : IModuleRegistration
{
    public void Register(IServiceCollection services, IConfiguration configuration)
    {
        // Identity is registered in the web host since it needs the shared DbContext
        // This module provides the ApplicationUser model and any identity-specific services
    }
}
