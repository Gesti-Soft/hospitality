namespace GestiSoft.Domain.Entities;

/// <summary>
/// Il gestore/azienda cliente di GestiSoft — radice reale del tenant. Un Cliente può avere
/// più Strutture (proprietà). Un Cliente vede solo le proprie Strutture; solo il Super Admin
/// vede tutti i Clienti.
/// </summary>
public class Cliente
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public string RagioneSociale { get; set; } = string.Empty;

    public string? PartitaIva { get; set; }

    public bool Attivo { get; set; } = true;

    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;

    public ICollection<Struttura> Strutture { get; set; } = new List<Struttura>();
}
