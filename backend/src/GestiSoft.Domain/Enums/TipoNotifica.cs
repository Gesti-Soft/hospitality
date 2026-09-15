namespace GestiSoft.Domain.Enums;

public enum TipoNotifica
{
    NuovaPrenotazione = 1,
    PrenotazioneAnnullata = 2,
    PrenotazioneModificata = 3,
    LicenzaInScadenza = 4,
    LicenzaScaduta = 5,
    SchedineInviate = 6,
    CheckOutDimenticato = 7,

    /// <summary>Schedina alloggiati non trasmessa entro il termine di legge: l'invio automatico non la prende più, va registrata a mano sul portale della Polizia di Stato (vedi TerminiSchedina).</summary>
    SchedinaFuoriTermine = 8,

    /// <summary>
    /// L'invio automatico verso una PA non è riuscito e i tentativi della giornata sono finiti
    /// (vedi PoliticaTentativi): si riprova domani, ma se la causa è una configurazione mancante
    /// nessuno se ne accorgerebbe — il Log lo direbbe, il campanello lo mette davanti agli occhi.
    /// </summary>
    InvioSchedineNonRiuscito = 9,

    /// <summary>
    /// Schedina con dati incompleti o non riconosciuti dall'anagrafica della PA: verrebbe rifiutata,
    /// quindi l'invio automatico non la prende. Va corretta, e il tempo per farlo è quello del
    /// termine di legge (vedi TerminiSchedina) — per questo si segnala subito, non a scadenza.
    /// </summary>
    SchedinaDatiIncompleti = 10,
}
