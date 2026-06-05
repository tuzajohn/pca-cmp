using Microsoft.AspNetCore.Mvc;
using PCA.Web.Models;
using PCA.Web.Services;

namespace PCA.Web.Controllers.Api;

[ApiController]
[Route("api/logs")]
public class LogsApiController : ControllerBase
{
    private readonly ILogService    _logService;
    private readonly IApiKeyService _apiKeyService;

    public LogsApiController(ILogService logService, IApiKeyService apiKeyService)
    {
        _logService    = logService;
        _apiKeyService = apiKeyService;
    }

    [HttpPost]
    public async Task<IActionResult> Ingest([FromHeader(Name = "X-Api-Key")] string? apiKey,
        [FromBody] ExternalLogRequest req)
    {
        if (string.IsNullOrWhiteSpace(apiKey))
            return Unauthorized(new { error = "Missing X-Api-Key header." });

        var key = await _apiKeyService.ValidateAsync(apiKey);
        if (key == null)
            return Unauthorized(new { error = "Invalid or revoked API key." });

        if (string.IsNullOrWhiteSpace(req.Message))
            return BadRequest(new { error = "Message is required." });

        await _logService.WriteAsync(new AppLog
        {
            Level      = NormalizeLevel(req.Level),
            Category   = "External",
            Source     = string.IsNullOrWhiteSpace(req.Source) ? key.AppName : req.Source,
            Message    = req.Message,
            Details    = req.Details,
            Action     = req.Action,
            EntityType = req.EntityType,
            EntityId   = req.EntityId,
            UserEmail  = req.UserEmail,
            IpAddress  = HttpContext.Connection.RemoteIpAddress?.ToString()
        });

        return Ok(new { status = "accepted" });
    }

    private static string NormalizeLevel(string? level) => level?.ToLower() switch
    {
        "warning" or "warn"    => "Warning",
        "error"                => "Error",
        "critical" or "fatal"  => "Critical",
        _                      => "Info"
    };
}

public class ExternalLogRequest
{
    public string? Level      { get; set; }
    public string? Source     { get; set; }
    public string  Message    { get; set; } = string.Empty;
    public string? Details    { get; set; }
    public string? Action     { get; set; }
    public string? EntityType { get; set; }
    public int?    EntityId   { get; set; }
    public string? UserEmail  { get; set; }
}
