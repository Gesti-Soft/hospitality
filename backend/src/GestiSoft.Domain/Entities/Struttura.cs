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
}
