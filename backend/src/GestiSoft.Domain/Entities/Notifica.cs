using GestiSoft.Domain.Common;
using GestiSoft.Domain.Enums;

namespace GestiSoft.Domain.Entities;

/// <summary>
/// Evento rilevante per l'operatore di una Struttura (nuova prenotazione, cancellazione, licenza in
/// scadenza, ecc.), mostrato come centro notifiche in-app — distinto da LogEvento, che è un log
/// tecnico consultabile solo da chi gestisce gli utenti. Qui vanno solo eventi azionabili/di
/// interesse operativo quotidiano.
/// </summary>
public class Notifica : TenantEntity
{
    public TipoNotifica Tipo { get; set; }

    public string Titolo { get; set; } = string.Empty;

    public string Messaggio { get; set; } = string.Empty;

    public Guid? PrenotazioneId { get; set; }

    public Prenotazione? Prenotazione { get; set; }

    /// <summary>Canale/agenzia di provenienza, quando pertinente (es. "Booking.com") — solo informativo.</summary>
    public string? Canale { get; set; }

    /// <summary>
    /// InAttesa solo per una cancellazione Wubook ancora nella finestra di grazia (vedi
    /// NotificaService.RegistraCancellazioneWubookAsync): non compare nelle liste/nel conteggio non
    /// lette finché non diventa Confermata (o viene fusa in una PrenotazioneModificata).
    /// </summary>
    public StatoNotifica Stato { get; set; } = StatoNotifica.Confermata;

    /// <summary>Valorizzato solo per Stato=InAttesa: scaduto questo momento senza una prenotazione corrispondente, la cancellazione diventa visibile.</summary>
    public DateTime? ScadenzaAttesaUtc { get; set; }

    public DateTime? LettaAtUtc { get; set; }

    /// <summary>
    /// Chiave di deduplica per le notifiche generate periodicamente da un job (es. "licenza-scadenza:
    /// {strutturaId}:{scadenza:yyyyMMdd}", "schedine:alloggiati-web:{strutturaId}:{oggi:yyyyMMdd}") —
    /// evita di ricrearne una identica ad ogni ciclo. Null per le notifiche generate da un'azione
    /// puntuale (nuova prenotazione, cancellazione) che non ne hanno bisogno.
    /// </summary>
    public string? ChiaveDedup { get; set; }
}
