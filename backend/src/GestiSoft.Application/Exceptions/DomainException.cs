namespace GestiSoft.Application.Exceptions;

/// <summary>
/// Base per gli errori di dominio/business attesi (non bug): il messaggio è già "sicuro" da
/// mostrare all'utente così com'è (comprensibile, non tecnico) — il middleware globale lo usa
/// direttamente nella risposta invece del messaggio generico riservato agli errori imprevisti.
/// </summary>
public abstract class DomainException(string userMessage, int statusCode) : Exception(userMessage)
{
    public string UserMessage { get; } = userMessage;

    public int StatusCode { get; } = statusCode;
}
