using System.Text.Json;
using Erp.Infrastructure.Middleware;
using FluentValidation;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace Erp.Tests.Infrastructure;

public class ExceptionHandlingMiddlewareTests
{
    [Fact]
    public async Task ValidationException_Returns400ProblemDetails()
    {
        var context = CreateContext();
        var middleware = new ExceptionHandlingMiddleware(
            _ => throw new ValidationException(
            [
                new FluentValidation.Results.ValidationFailure("Email", "El email es obligatorio"),
            ]),
            NullLogger<ExceptionHandlingMiddleware>.Instance);

        await middleware.InvokeAsync(context);

        Assert.Equal(400, context.Response.StatusCode);

        var problem = await ReadProblem(context);
        Assert.Equal("Error de validación", problem.GetProperty("title").GetString());
        Assert.True(problem.TryGetProperty("errors", out var errors));
        Assert.True(errors.TryGetProperty("Email", out _));
    }

    [Fact]
    public async Task KeyNotFoundException_Returns404()
    {
        var context = CreateContext();
        var middleware = new ExceptionHandlingMiddleware(
            _ => throw new KeyNotFoundException("Factura no encontrada"),
            NullLogger<ExceptionHandlingMiddleware>.Instance);

        await middleware.InvokeAsync(context);

        Assert.Equal(404, context.Response.StatusCode);
        var problem = await ReadProblem(context);
        Assert.Equal("Recurso no encontrado", problem.GetProperty("title").GetString());
    }

    [Fact]
    public async Task UnhandledException_Returns500()
    {
        var context = CreateContext();
        var middleware = new ExceptionHandlingMiddleware(
            _ => throw new InvalidProgramException("fallo inesperado"),
            NullLogger<ExceptionHandlingMiddleware>.Instance);

        await middleware.InvokeAsync(context);

        Assert.Equal(500, context.Response.StatusCode);
        var problem = await ReadProblem(context);
        Assert.Equal("Error interno del servidor", problem.GetProperty("title").GetString());
    }

    private static DefaultHttpContext CreateContext()
    {
        var context = new DefaultHttpContext();
        context.Response.Body = new MemoryStream();
        context.Request.Path = "/api/test";
        context.Request.Method = "GET";
        return context;
    }

    private static async Task<JsonElement> ReadProblem(HttpContext context)
    {
        context.Response.Body.Seek(0, SeekOrigin.Begin);
        return await JsonSerializer.DeserializeAsync<JsonElement>(context.Response.Body);
    }
}
