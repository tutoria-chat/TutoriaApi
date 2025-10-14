using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Extensions;
using Microsoft.Extensions.Logging;
using System.Diagnostics;
using System.Text;

namespace TutoriaApi.Infrastructure.Middleware;

/// <summary>
/// Middleware to log HTTP requests and responses for debugging and monitoring.
/// </summary>
public class RequestResponseLoggingMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<RequestResponseLoggingMiddleware> _logger;

    public RequestResponseLoggingMiddleware(RequestDelegate next, ILogger<RequestResponseLoggingMiddleware> logger)
    {
        _next = next;
        _logger = logger;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        // Skip logging for health check endpoints
        if (context.Request.Path.StartsWithSegments("/health"))
        {
            await _next(context);
            return;
        }

        var requestId = Guid.NewGuid().ToString();
        var stopwatch = Stopwatch.StartNew();

        // Log request
        await LogRequest(context, requestId);

        // Copy original response body stream
        var originalBodyStream = context.Response.Body;

        using var responseBody = new MemoryStream();
        context.Response.Body = responseBody;

        try
        {
            await _next(context);
        }
        finally
        {
            stopwatch.Stop();

            // Log response
            await LogResponse(context, requestId, stopwatch.ElapsedMilliseconds);

            // Copy response back to original stream
            await responseBody.CopyToAsync(originalBodyStream);
        }
    }

    private async Task LogRequest(HttpContext context, string requestId)
    {
        var request = context.Request;

        var logMessage = new StringBuilder();
        logMessage.AppendLine($"[{requestId}] HTTP Request");
        logMessage.AppendLine($"Method: {request.Method}");
        logMessage.AppendLine($"Path: {request.Path}");
        logMessage.AppendLine($"QueryString: {request.QueryString}");
        logMessage.AppendLine($"ContentType: {request.ContentType}");
        logMessage.AppendLine($"ContentLength: {request.ContentLength}");

        // Log headers (excluding sensitive ones)
        logMessage.AppendLine("Headers:");
        foreach (var header in request.Headers)
        {
            // Skip sensitive headers
            if (header.Key.Equals("Authorization", StringComparison.OrdinalIgnoreCase) ||
                header.Key.Equals("Cookie", StringComparison.OrdinalIgnoreCase))
            {
                logMessage.AppendLine($"  {header.Key}: [REDACTED]");
            }
            else
            {
                logMessage.AppendLine($"  {header.Key}: {header.Value}");
            }
        }

        // Note: Body logging disabled to avoid buffering complexity in ASP.NET Core 2.x
        // For full body logging, upgrade to ASP.NET Core 3.0+ and use request.EnableBuffering()

        _logger.LogInformation(logMessage.ToString());
    }

    private async Task LogResponse(HttpContext context, string requestId, long elapsedMs)
    {
        context.Response.Body.Seek(0, SeekOrigin.Begin);
        var responseBody = await new StreamReader(context.Response.Body).ReadToEndAsync();
        context.Response.Body.Seek(0, SeekOrigin.Begin);

        var logMessage = new StringBuilder();
        logMessage.AppendLine($"[{requestId}] HTTP Response");
        logMessage.AppendLine($"StatusCode: {context.Response.StatusCode}");
        logMessage.AppendLine($"ContentType: {context.Response.ContentType}");
        logMessage.AppendLine($"Elapsed: {elapsedMs}ms");

        // Log response body for small responses (avoid logging large files)
        if (responseBody.Length < 4096)
        {
            logMessage.AppendLine($"Body: {responseBody}");
        }
        else
        {
            logMessage.AppendLine($"Body: [LARGE RESPONSE - {responseBody.Length} bytes]");
        }

        // Log level based on status code
        if (context.Response.StatusCode >= 500)
        {
            _logger.LogError(logMessage.ToString());
        }
        else if (context.Response.StatusCode >= 400)
        {
            _logger.LogWarning(logMessage.ToString());
        }
        else
        {
            _logger.LogInformation(logMessage.ToString());
        }
    }
}
