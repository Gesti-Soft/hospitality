namespace GestiSoft.Application.Fatturazione.Sdi;

/// <summary>
/// Il servizio SdI visto dall'Api: un altro processo, l'unico che ha i certificati del canale. L'Api
/// controlla chi chiede l'invio, il servizio lo fa e aggiorna la fattura.
/// </summary>
public interface IServizioSdi
{
    /// <exception cref="Exceptions.ConflictException">Il servizio ha rifiutato l'invio (con il motivo), oppure non ha risposto.</exception>
    Task<EsitoInvioSdi> InviaFatturaAsync(Guid strutturaId, Guid fatturaId, string? operatore, CancellationToken cancellationToken);
}

/// <summary>La richiesta interna Api -> servizio SdI.</summary>
public record RichiestaInvioSdi(Guid StrutturaId, Guid FatturaId, string? Operatore);
