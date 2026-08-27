namespace GestiSoft.Application.AlloggiatiWeb;

public record AlloggiatiWebTokenRisultato(bool Ok, string? Token, string? Errore);

public record AlloggiatiWebInvioRisultato(bool Ok, string? ErroreCodice, string? ErroreDescrizione, string? ErroreDettaglio);

/// <summary>
/// Client SOAP verso il servizio "Alloggiati Web" della Polizia di Stato — porta
/// StatePoliceApiRepository.GetToken/SendSchedina del legacy (envelope SOAP 1.1 scritto a mano
/// con System.Xml.Linq, stesso approccio già usato per l'XML SDI in Fase 4 e per l'XML-RPC Wubook
/// in Fase 5, nessuna libreria SOAP esterna). Ogni fallimento di trasporto viene intercettato
/// dall'implementazione e tradotto in un risultato "non ok" invece di propagare l'eccezione —
/// stesso principio stabilito in Fase 2/5 per non far risalire un 500 grezzo all'utente.
/// </summary>
public interface IAlloggiatiWebClient
{
    Task<AlloggiatiWebTokenRisultato> GenerateTokenAsync(string utente, string password, string wsKey, CancellationToken cancellationToken);

    Task<AlloggiatiWebInvioRisultato> SendAsync(string utente, string token, IReadOnlyList<string> schedine, CancellationToken cancellationToken);
}
