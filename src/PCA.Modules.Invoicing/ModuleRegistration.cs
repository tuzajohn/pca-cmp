using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using PCA.Modules.Invoicing.Services;

namespace PCA.Modules.Invoicing;

public class ModuleRegistration
{
    public void Register(IServiceCollection services, IConfiguration config)
    {
        services.AddScoped<IInvoicingService, InvoicingService>();

        var ipps = config.GetSection("Invoicing:IppsDb").Get<ExternalDbSettings>()
                   ?? new ExternalDbSettings();
        var hcm  = config.GetSection("Invoicing:HcmDb").Get<ExternalDbSettings>()
                   ?? new ExternalDbSettings();

        services.AddSingleton(new InvoiceDataService(ipps, hcm));
    }
}
