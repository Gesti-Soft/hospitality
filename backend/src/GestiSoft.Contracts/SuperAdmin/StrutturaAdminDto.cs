namespace GestiSoft.Contracts.SuperAdmin;

// I 4 flag *Abilitato sono concessi dal Super Admin per QUESTA struttura — distinti dai flag
// *Attivo/a sopra, che sono il toggle self-service del Cliente.
// Attivo: false = struttura "eliminata" (soft-delete) dal Cliente stesso o dal Super Admin.
public record StrutturaAdminDto(
    Guid Id,
    string Nome,
    bool Attivo,
    DateTime? DisattivataAtUtc,
    bool WubookAttivo,
    string? WubookUltimoErrore,
    DateTime? ScadenzaLicenza,
    bool PoliziaStatoAttiva,
    bool OsservatorioAttivo,
    bool PayTouristAttivo,
    bool WubookAbilitato,
    bool AlloggiatiWebAbilitato,
    bool OsservatorioAbilitato,
    bool PayTouristAbilitato);
