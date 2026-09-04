namespace GestiSoft.Contracts.Strutture;

public record StrutturaDto(
    Guid Id,
    Guid ClienteId,
    string Nome,
    DateTime CreatedAtUtc,
    bool WubookAbilitato,
    bool AlloggiatiWebAbilitato,
    bool OsservatorioAbilitato,
    bool PayTouristAbilitato,
    // True se la licenza software GestiSoft di questa Struttura è scaduta (NON la licenza Wubook) — la Struttura è inaccessibile finché il Super Admin non la rinnova (vedi TenantAccessGuard).
    bool LicenzaScaduta,
    // False = struttura "eliminata" (soft-delete) — compare in questo elenco solo per il Super Admin, che vi ha comunque libero accesso.
    bool Attivo);

public record ImpostaAttivoStrutturaRequest(bool Attivo);
