using GestiSoft.Application.Auth;
using GestiSoft.Application.Exceptions;
using GestiSoft.Application.Logging;
using GestiSoft.Domain.Enums;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.DependencyInjection;

namespace GestiSoft.Api.Middleware;

/// <summary>
/// Gestore globale delle eccezioni: al client risponde con un messaggio corretto e comprensibile
/// ma non tecnico (mai stack trace/nomi di tabelle/eccezioni .NET), nei log (Serilog + tabella
/// LogEvento per gli errori 500) va invece il dettaglio completo, collegato allo stesso
/// correlationId mostrato all'utente così il supporto può ritrovarlo.
/// </summary>
// Registrato come singleton (AddExceptionHandler<T>): i servizi scoped (ILogEventoService,
// ICurrentUser) non possono essere iniettati nel costruttore e vanno risolti da
// HttpContext.RequestServices per ogni richiesta.
public class GlobalExceptionHandler(ILogger<GlobalExceptionHandler> logger) : IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(HttpContext httpContext, Exception exception, CancellationToken cancellationToken)
    {
        var correlationId = httpContext.TraceIdentifier;

        var (statusCode, userMessage) = exception switch
        {
            DomainException domainEx => (domainEx.StatusCode, domainEx.UserMessage),
            _ => (StatusCodes.Status500InternalServerError,
                $"Si è verificato un errore imprevisto. Riferimento: {correlationId}"),
        };

        logger.LogError(
            exception,
            "Richiesta {Method} {Path} fallita con status {StatusCode}. CorrelationId={CorrelationId}",
            httpContext.Request.Method,
            httpContext.Request.Path,
            statusCode,
            correlationId);

        // Solo gli errori davvero imprevisti (500) finiscono nella tabella consultabile da UI:
        // un 404/409 "atteso" (es. camera già prenotata) non è un incidente da investigare.
        if (statusCode >= StatusCodes.Status500InternalServerError)
        {
            try
            {
                var logEventoService = httpContext.RequestServices.GetRequiredService<ILogEventoService>();
                var currentUser = httpContext.RequestServices.GetRequiredService<ICurrentUser>();

                await logEventoService.RegistraAsync(
                    livello: LivelloLog.Error,
                    messaggio: userMessage,
                    origine: "Api",
                    dettaglio: exception.ToString(),
                    correlationId: correlationId,
                    clienteId: currentUser.IsAuthenticated ? currentUser.ClienteId : null,
                    cancellationToken: cancellationToken);
            }
            catch (Exception loggingException)
            {
                // Non far fallire la gestione dell'errore originale se anche la scrittura del log fallisce.
                logger.LogError(loggingException, "Impossibile registrare il LogEvento per CorrelationId={CorrelationId}", correlationId);
            }
        }

        var problemDetails = new ProblemDetails
        {
            Status = statusCode,
            Title = statusCode >= StatusCodes.Status500InternalServerError ? "Errore imprevisto" : "Richiesta non valida",
            Detail = userMessage,
        };
        problemDetails.Extensions["correlationId"] = correlationId;

        httpContext.Response.StatusCode = statusCode;
        await httpContext.Response.WriteAsJsonAsync(problemDetails, cancellationToken: cancellationToken);

        return true;
    }
}
