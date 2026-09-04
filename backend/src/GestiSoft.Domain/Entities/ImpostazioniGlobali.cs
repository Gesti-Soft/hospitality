namespace GestiSoft.Domain.Entities;

/// <summary>
/// Impostazioni a livello di applicazione, non di Cliente/Struttura — una sola riga per tutto
/// GestiSoft. Gestite solo dal Super Admin.
/// </summary>
public class ImpostazioniGlobali
{
    public Guid Id { get; set; } = Guid.NewGuid();

    /// <summary>Id Software richiesto dall'API PayTourist per identificare GestiSoft come software integrato — uno solo per tutta l'applicazione, non per Struttura.</summary>
    public int? IdSoftwarePaytourist { get; set; }

    /// <summary>Token Wubook (apikey dell'account partner) — uguale per tutte le Strutture, non per Struttura (confermato sui dati reali: stesso identico valore ovunque).</summary>
    public string? TokenWubook { get; set; }
}
