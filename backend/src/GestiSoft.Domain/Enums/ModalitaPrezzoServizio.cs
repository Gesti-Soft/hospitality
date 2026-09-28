namespace GestiSoft.Domain.Enums;

/// <summary>
/// Come si conta il prezzo di un servizio extra. "A persona" moltiplica per le persone indicate
/// sulla riga, "a notte" per le notti del soggiorno.
/// </summary>
public enum ModalitaPrezzoServizio
{
    APersonaANotte = 1,
    APersona = 2,
    ANotte = 3,
    APrenotazione = 4,
}
