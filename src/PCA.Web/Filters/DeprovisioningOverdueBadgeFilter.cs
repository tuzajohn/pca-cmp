using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using PCA.Modules.AccessManagement.Services;

namespace PCA.Web.Filters;

public class DeprovisioningOverdueBadgeFilter : IAsyncActionFilter
{
    private readonly IAccessManagementService _svc;

    public DeprovisioningOverdueBadgeFilter(IAccessManagementService svc)
    {
        _svc = svc;
    }

    public async Task OnActionExecutionAsync(ActionExecutingContext context, ActionExecutionDelegate next)
    {
        var result = await next();

        if (result.Result is ViewResult viewResult && context.HttpContext.User.Identity?.IsAuthenticated == true)
        {
            var overdue = await _svc.GetOverdueDeprovisioningEventsAsync();
            viewResult.ViewData["DeprovisioningOverdueCount"] = overdue.Count;
        }
    }
}
