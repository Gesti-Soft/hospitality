using GestiSoft.Domain.Common;
using GestiSoft.Domain.Enums;

namespace GestiSoft.Domain.Entities;

/// <summary>
/// Servizio extra venduto con una prenotazione. Nome, modalità e prezzo sono copiati dal listino al
/// momento dell'aggiunta: un cambio di listino, o l'eliminazione del servizio, non tocca il conto di
/// una prenotazione già fatta. Lo stesso servizio può comparire più volte in giorni diversi (la SPA
/// il 12 per due persone e il 14 per una).
/// </summary>
public class PrenotazioneServizio : TenantEntity
{
    public Guid PrenotazioneId { get; set; }

    public Guid ServizioId { get; set; }

    public string Nome { get; set; } = string.Empty;

    public ModalitaPrezzoServizio Modalita { get; set; }

    public decimal PrezzoUnitario { get; set; }

    /// <summary>Persone per i servizi "a persona", altrimenti unità (es. due posti auto).</summary>
    public int Quantita { get; set; } = 1;

    /// <summary>
    /// Data civile del servizio (senza fuso orario, come le date del soggiorno). Per i servizi a notte
    /// è la prima notte e <see cref="Al"/> il giorno dopo l'ultima, come check-in e check-out: dal 12
    /// al 15 sono tre notti.
    /// </summary>
    public DateOnly Dal { get; set; }

    /// <inheritdoc cref="Dal"/>
    public DateOnly? Al { get; set; }

    public OrigineServizio Origine { get; set; } = OrigineServizio.ConLaPrenotazione;

    /// <summary>Chi l'ha aggiunto (l'email, come l'operatore nel log): per le contestazioni al check-out.</summary>
    public string? AggiuntoDa { get; set; }
}
