using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
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

        services.AddSingleton(sp => new InvoiceDataService(
            ipps, hcm,
            sp.GetRequiredService<ILogger<InvoiceDataService>>()));

        services.AddScoped(sp => new CrbReportService(
            ipps,
            sp.GetRequiredService<ILogger<CrbReportService>>()));
    }
}
