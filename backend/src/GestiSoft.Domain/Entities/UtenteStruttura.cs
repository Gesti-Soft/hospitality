using GestiSoft.Domain.Enums;

namespace GestiSoft.Domain.Entities;

/// <summary>
/// Assegnazione di un Utente a una Struttura con relativo ruolo e permessi. Porta 1:1 i flag
/// booleani di OrderManagement.Model.BusinesObject.Permissions del sistema legacy, ma scoped per
/// coppia Utente/Struttura invece che per Utente globale: lo stesso utente può avere permessi
/// diversi su Strutture diverse dello stesso Cliente (es. Manager ovunque, Receptionist solo su
/// una struttura specifica).
/// </summary>
public class UtenteStruttura
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public Guid UtenteId { get; set; }

    public Utente? Utente { get; set; }

    public Guid StrutturaId { get; set; }

    public Struttura? Struttura { get; set; }

    public RuoloUtente Ruolo { get; set; }

    public bool BookingRead { get; set; }

    public bool BookingWrite { get; set; }

    public bool ReservationRead { get; set; }

    public bool ReservationWrite { get; set; }

    public bool StatePoliceRead { get; set; }

    public bool StatePoliceWrite { get; set; }

    public bool StatePoliceSettings { get; set; }

    public bool SettingAgency { get; set; }

    public bool SettingUser { get; set; }

    public bool SettingRoomRead { get; set; }

    public bool SettingRoomWrite { get; set; }

    public bool RoomStatusUpdate { get; set; }

    public bool FinanceRead { get; set; }

    public bool FinanceWrite { get; set; }

    public bool RestaurantRead { get; set; }

    public bool RestaurantWrite { get; set; }
}
