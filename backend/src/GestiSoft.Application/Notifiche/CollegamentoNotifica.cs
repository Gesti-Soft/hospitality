using GestiSoft.Domain.Entities;
using GestiSoft.Domain.Enums;

namespace GestiSoft.Application.Notifiche;

/// <summary>
/// La pagina del gestionale che una notifica apre al clic: dove c'è qualcosa da guardare o da fare.
/// Null per quelle solo informative (licenza, schedine inviate, aggiornamento), che si segnano lette
/// e basta. Si ricava da tipo, prenotazione e chiave di deduplica, quindi vale anche per le
/// notifiche già salvate. Chi non ha il permesso per la pagina viene fermato lì, come da menu.
/// </summary>
public static class CollegamentoNotifica
{
    public static string? Per(Notifica n) => n.Tipo switch
    {
        TipoNotifica.NuovaPrenotazione or TipoNotifica.PrenotazioneModificata or TipoNotifica.PrenotazioneAnnullata
            => n.PrenotazioneId is { } id ? $"/calendario?prenotazione={id}" : "/calendario",
        TipoNotifica.CheckOutDimenticato => "/check-in-out",
        // Le schedine fuori termine o incomplete sono quelle di Alloggiati Web: si sistemano e si inviano da lì.
        TipoNotifica.SchedinaFuoriTermine or TipoNotifica.SchedinaDatiIncompleti => "/polizia-di-stato",
        TipoNotifica.InvioSchedineNonRiuscito => PaginaServizio(n.ChiaveDedup),
        TipoNotifica.PulizieDaFare => "/pulizie",
        TipoNotifica.EsitoFatturaSdi => "/fatturazione",
        TipoNotifica.FatturaRicevuta => "/fatturazione?tab=fatture-ricevute",
        TipoNotifica.TicketChiuso => TicketId(n.ChiaveDedup) is { } ticket ? $"/assistenza?ticket={ticket}" : "/assistenza",
        _ => null,
    };

    /// <summary>"invio-non-riuscito:{servizio}:…": il servizio dice quale pagina di invio aprire.</summary>
    private static string? PaginaServizio(string? chiave) => chiave?.Split(':') switch
    {
        [_, "AlloggiatiWeb", ..] => "/polizia-di-stato",
        [_, "Osservatorio Turistico", ..] => "/osservatorio",
        [_, "PayTourist", ..] => "/paytourist",
        _ => null,
    };

    /// <summary>"ticket-chiuso:{id}".</summary>
    private static Guid? TicketId(string? chiave) =>
        chiave?.Split(':') is ["ticket-chiuso", var id] && Guid.TryParse(id, out var ticket) ? ticket : null;
}
