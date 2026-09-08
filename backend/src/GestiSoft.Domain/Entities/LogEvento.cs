using GestiSoft.Domain.Common;
using GestiSoft.Domain.Enums;

namespace GestiSoft.Domain.Entities;

/// <summary>
/// Log applicativo consultabile da UI (filtrabile per Cliente/Struttura), distinto dal log
/// tecnico dettagliato di Serilog su stdout. Qui vanno eventi rilevanti per l'utente/supporto
/// (errori di sistema con CorrelationId, esiti delle integrazioni esterne), non ogni riga di
/// trace. ClienteId/StrutturaId sono nullable: un errore può avvenire prima di sapere chi è
/// l'utente (es. fallimento login).
/// </summary>
public class LogEvento : Entity
{
    public Guid? ClienteId { get; set; }

    public Guid? StrutturaId { get; set; }

    public LivelloLog Livello { get; set; }

    /// <summary>Messaggio comprensibile, lo stesso mostrato/mostrabile all'utente.</summary>
    public string Messaggio { get; set; } = string.Empty;

    /// <summary>Dettaglio tecnico completo (eccezione, stack trace, payload) — solo per consultazione interna/supporto.</summary>
    public string? Dettaglio { get; set; }

    /// <summary>Collega questo log all'ID mostrato all'utente in caso di errore di sistema.</summary>
    public string? CorrelationId { get; set; }

    /// <summary>Origine dell'evento: "Api", "Worker", nome dell'integrazione, ecc.</summary>
    public string Origine { get; set; } = string.Empty;

    /// <summary>Categoria per filtrare in UI: "Prenotazione", "Wubook", "AlloggiatiWeb", "Osservatorio", "PayTourist", "Backup" (scritta direttamente dagli script PowerShell del backup automatico via psql, non dall'app), "Sistema"...</summary>
    public string? Categoria { get; set; }

    /// <summary>Email dell'utente che ha compiuto l'azione — null per eventi di sistema/job automatici (nessun utente coinvolto).</summary>
    public string? Operatore { get; set; }

    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
}
