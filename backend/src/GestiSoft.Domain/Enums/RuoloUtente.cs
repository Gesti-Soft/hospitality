namespace GestiSoft.Domain.Enums;

/// <summary>
/// Ruolo dell'utente su una specifica Struttura (etichetta descrittiva — l'autorizzazione vera
/// e propria è determinata dai flag di permesso su UtenteStruttura, non dal ruolo in sé, fedele
/// al modello del sistema legacy dove Role_Id e Permissions erano indipendenti).
/// </summary>
public enum RuoloUtente
{
    Administrator = 1,
    Receptionist = 2,
    Housekeeper = 3,
    Manager = 4,
    Accountant = 5,
    Maintenance = 6,
    FnbManager = 7,
    BookingAgent = 8,
    NightAuditor = 9,
    Marketing = 10,
    Owner = 11,
}
