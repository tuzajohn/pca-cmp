using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using PCA.Modules.Approvals.Services;
using PCA.Shared;

namespace PCA.Modules.Approvals;

public class ModuleRegistration : IModuleRegistration
{
    public void Register(IServiceCollection services, IConfiguration configuration)
    {
        services.AddScoped<IApprovalService, ApprovalService>();
    }
}
