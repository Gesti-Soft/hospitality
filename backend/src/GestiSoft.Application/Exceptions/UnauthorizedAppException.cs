namespace GestiSoft.Application.Exceptions;

/// <summary>Nome non "UnauthorizedException" per non confondersi con System.UnauthorizedAccessException.</summary>
public class UnauthorizedAppException(string userMessage, string? assistenza = null) : DomainException(userMessage, statusCode: 401)
{
    /// <summary>
    /// A chi deve rivolgersi questo utente per farsi rimettere dentro, calcolato sul suo ruolo
    /// (vedi AuthService.IndicazioneAssistenzaAsync): ognuno sale di un gradino nella propria
    /// catena, e solo il titolare dell'account arriva a GestiSoft. Null quando non si sa chi sia
    /// (email sconosciuta) o quando l'errore non riguarda l'accesso.
    /// </summary>
    public string? Assistenza { get; } = assistenza;
}
