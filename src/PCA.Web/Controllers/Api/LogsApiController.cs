using Microsoft.AspNetCore.Mvc;
using PCA.Web.Models;
using PCA.Web.Services;

namespace PCA.Web.Controllers.Api;

[ApiController]
[Route("api/logs")]
public class LogsApiController : ControllerBase
{
    private readonly ILogService _logService;
    private readonly IConfiguration _config;

    public LogsApiController(ILogService logService, IConfiguration config)
    {
        _logService = logService;
        _config = config;
    }

    [HttpPost]
    public async Task<IActionResult> Ingest([FromHeader(Name = "X-Api-Key")] string? apiKey,
        [FromBody] ExternalLogRequest req)
    {
        var expectedKey = _config["Logging:ExternalApiKey"];
        if (string.IsNullOrEmpty(expectedKey) || apiKey != expectedKey)
            return Unauthorized(new { error = "Invalid or missing API key." });

        if (string.IsNullOrWhiteSpace(req.Message))
            return BadRequest(new { error = "Message is required." });

        await _logService.WriteAsync(new AppLog
        {
            Level      = NormalizeLevel(req.Level),
            Category   = "External",
            Source     = string.IsNullOrWhiteSpace(req.Source) ? "Unknown" : req.Source,
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
        "warning" or "warn" => "Warning",
        "error"             => "Error",
        "critical" or "fatal" => "Critical",
        _                   => "Info"
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
