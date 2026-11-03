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
        var hcmApi = config.GetSection("Invoicing:HcmApi").Get<HcmApiSettings>()
                     ?? new HcmApiSettings();

        services.AddSingleton(hcmApi);
        services.AddHttpClient<HcmSalaryApiClient>(c =>
            c.Timeout = TimeSpan.FromSeconds(Math.Max(1, hcmApi.TimeoutSeconds)));

        services.AddSingleton(sp => new InvoiceDataService(
            ipps, hcm,
            sp.GetRequiredService<ILogger<InvoiceDataService>>()));

        services.AddScoped(sp => new CrbReportService(
            ipps,
            sp.GetRequiredService<ILogger<CrbReportService>>()));

        services.AddScoped<HcmMappingService>();

        services.AddScoped(sp => new HcmReportService(
            hcm,
            sp.GetRequiredService<HcmMappingService>(),
            sp.GetRequiredService<CrbReportService>(),
            sp.GetRequiredService<HcmSalaryApiClient>(),
            sp.GetRequiredService<ILogger<HcmReportService>>()));
    }
}
