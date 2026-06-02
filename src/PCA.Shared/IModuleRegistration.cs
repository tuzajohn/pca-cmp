using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace PCA.Shared;

public interface IModuleRegistration
{
    void Register(IServiceCollection services, IConfiguration configuration);
}
