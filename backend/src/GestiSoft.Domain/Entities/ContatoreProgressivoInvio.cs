namespace GestiSoft.Domain.Entities;

/// <summary>
/// L'ultimo progressivo usato nei nomi dei file per lo SdI da un codice fiscale trasmittente. Il
/// contatore è del <b>soggetto</b>, non del programma né della struttura: un nome file già usato da
/// quel codice fiscale (anche dal portale dell'Agenzia o da un altro software) viene scartato con
/// l'errore 00002, a prescindere dall'esito precedente. Per questo non torna mai indietro, parte
/// sopra qualunque valore possa essere già stato speso, e il titolare del canale ha un contatore
/// solo anche se è pure cliente del gestionale. Tabella globale: il codice fiscale è la chiave.
/// </summary>
public class ContatoreProgressivoInvio
{
    public string CodiceFiscale { get; set; } = string.Empty;

    public int UltimoProgressivo { get; set; }

    public DateTime UpdatedAtUtc { get; set; } = DateTime.UtcNow;
}
