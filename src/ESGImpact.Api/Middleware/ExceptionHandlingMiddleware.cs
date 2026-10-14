using System.Text.Json;
using ESGImpact.Api.Exceptions;
using Microsoft.AspNetCore.Mvc;
using MongoDB.Driver;

namespace ESGImpact.Api.Middleware;

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
            await HandleAsync(context, ex);
        }
    }

    private async Task HandleAsync(HttpContext context, Exception ex)
    {
        var (status, title) = ex switch
        {
            DomainValidationException => (StatusCodes.Status400BadRequest, "Requisição inválida"),
            NotFoundException => (StatusCodes.Status404NotFound, "Recurso não encontrado"),
            FormatException => (StatusCodes.Status400BadRequest, "Documento JSON inválido"),
            // violação de índice único (ex.: dois fatores com o mesmo `code`)
            MongoWriteException mwe when mwe.WriteError?.Category == ServerErrorCategory.DuplicateKey
                => (StatusCodes.Status409Conflict, "Documento duplicado"),
            MongoCommandException mce when mce.Code == 121
                => (StatusCodes.Status400BadRequest, "Documento rejeitado pelo validador $jsonSchema"),
            _ => (StatusCodes.Status500InternalServerError, "Erro interno do servidor")
        };

        if (status == StatusCodes.Status500InternalServerError)
            _logger.LogError(ex, "Erro não tratado ao processar {Path}", context.Request.Path);

        var problem = new ProblemDetails
        {
            Status = status,
            Title = title,
            Detail = status == StatusCodes.Status500InternalServerError
                ? "Ocorreu um erro inesperado. Tente novamente mais tarde."
                : ex.Message,
            Instance = context.Request.Path
        };

        context.Response.StatusCode = status;
        context.Response.ContentType = "application/problem+json";
        await context.Response.WriteAsync(JsonSerializer.Serialize(problem));
    }
}
