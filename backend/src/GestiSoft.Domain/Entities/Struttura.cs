namespace GestiSoft.Domain.Entities;

/// <summary>
/// Una struttura ricettiva (hotel/B&amp;B/casa vacanze), appartenente a un Cliente.
/// Le entità operative (camere, prenotazioni, ospiti, ecc.) puntano a questa entità
/// tramite StrutturaId — è la chiave di scoping principale dei dati.
/// Un Cliente può avere più Strutture; l'accesso di un Utente a una Struttura e il relativo
/// ruolo sono definiti in UtenteStruttura.
/// </summary>
public class Struttura
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public Guid ClienteId { get; set; }

    public Cliente? Cliente { get; set; }

    public string Nome { get; set; } = string.Empty;

    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// "Eliminazione" di una Struttura, in realtà un soft-delete: nessun dato collegato (camere,
    /// prenotazioni, ospiti, fatture, ecc.) viene mai cancellato. Se false, la Struttura sparisce
    /// dai selettori/liste operative e nessun utente può più operarci (TenantAccessGuard), ma resta
    /// consultabile/riattivabile via database se necessario — mai una vera DELETE.
    /// </summary>
    public bool Attivo { get; set; } = true;

    /// <summary>
    /// Momento in cui la Struttura è stata disattivata (null se non è mai stata disattivata, o se è
    /// stata riattivata). Usato dal Super Admin per individuare le Strutture disattivate da oltre 90
    /// giorni ed eliminarle definitivamente (vedi SuperAdminService).
    /// </summary>
    public DateTime? DisattivataAtUtc { get; set; }

    /// <summary>
    /// Concessione dei servizi esterni da parte del Super Admin, per singola Struttura (non più per
    /// l'intero Cliente: due Strutture dello stesso Cliente possono avere concessioni diverse, es.
    /// contratti/licenze distinti). Se false, questa Struttura non deve poterlo usare né attivare in
    /// alcun modo (indipendentemente dai toggle self-service su ImpostazioniStruttura/
    /// WubookIntegrazione), e nel frontend la relativa voce di menu/schermata non deve comparire
    /// affatto. Default false: una Struttura appena creata parte senza servizi — è il Super Admin a
    /// concederli esplicitamente in un secondo momento, non un'attivazione automatica.
    /// </summary>
    public bool WubookAbilitato { get; set; }

    public bool AlloggiatiWebAbilitato { get; set; }

    public bool OsservatorioAbilitato { get; set; }

    /// <summary>PayTourist in particolare dipende dal Comune: non tutti i comuni l'hanno adottato.</summary>
    public bool PayTouristAbilitato { get; set; }

    /// <summary>
    /// Scadenza della licenza software GestiSoft assegnata dal Super Admin a questa Struttura — è la
    /// licenza che GestiSoft concede al Cliente per usare il gestionale, indipendente da quali
    /// integrazioni esterne (Wubook/Alloggiati Web/Osservatorio/PayTourist) siano concesse: NON è la
    /// licenza Wubook (quella è solo il Codice struttura/lcode, vedi WubookIntegrazione). Oltre questa
    /// data, TenantAccessGuard blocca l'uso della Struttura per ogni suo utente e AuthService blocca
    /// il login di chi non ha nessun'altra Struttura ancora valida — mai per una scadenza mai
    /// impostata (null), solo per una data reale già superata.
    /// </summary>
    public DateTime? ScadenzaLicenza { get; set; }
}
