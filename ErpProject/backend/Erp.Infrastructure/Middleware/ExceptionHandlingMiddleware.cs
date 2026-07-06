using System.Net;
using FluentValidation;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;

namespace Erp.Infrastructure.Middleware;

/// <summary>
/// Convierte excepciones no controladas en respuestas ProblemDetails consistentes.
/// </summary>
public class ExceptionHandlingMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<ExceptionHandlingMiddleware> _logger;

    public ExceptionHandlingMiddleware(RequestDelegate next, ILogger<ExceptionHandlingMiddleware> logger)
    {
        _next = next;
        _logger = logger;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        try
        {
            await _next(context);
        }
        catch (Exception ex)
        {
            await HandleExceptionAsync(context, ex);
        }
    }

    private async Task HandleExceptionAsync(HttpContext context, Exception ex)
    {
        var (status, title) = ex switch
        {
            ValidationException => (HttpStatusCode.BadRequest, "Error de validación"),
            UnauthorizedAccessException => (HttpStatusCode.Unauthorized, "No autorizado"),
            KeyNotFoundException => (HttpStatusCode.NotFound, "Recurso no encontrado"),
            InvalidOperationException => (HttpStatusCode.BadRequest, "Operación no válida"),
            ArgumentException => (HttpStatusCode.BadRequest, "Solicitud no válida"),
            _ => (HttpStatusCode.InternalServerError, "Error interno del servidor")
        };

        if (status == HttpStatusCode.InternalServerError)
            _logger.LogError(ex, "Unhandled exception on {Method} {Path}", context.Request.Method, context.Request.Path);
        else
            _logger.LogWarning(ex, "{Title} on {Method} {Path}", title, context.Request.Method, context.Request.Path);

        context.Response.StatusCode = (int)status;
        context.Response.ContentType = "application/problem+json";

        var problem = new ProblemDetails
        {
            Status = (int)status,
            Title = title,
            Detail = ex.Message,
            Instance = context.Request.Path
        };

        if (ex is ValidationException validationEx)
        {
            problem.Extensions["errors"] = validationEx.Errors
                .GroupBy(e => e.PropertyName)
                .ToDictionary(g => g.Key, g => g.Select(e => e.ErrorMessage).ToArray());
        }

        await context.Response.WriteAsJsonAsync(problem);
    }
}
