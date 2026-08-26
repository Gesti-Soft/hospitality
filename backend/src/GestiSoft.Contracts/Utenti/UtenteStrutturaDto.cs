using GestiSoft.Domain.Enums;

namespace GestiSoft.Contracts.Utenti;

public record UtenteStrutturaDto(
    Guid Id,
    Guid UtenteId,
    Guid StrutturaId,
    RuoloUtente Ruolo,
    bool BookingRead,
    bool BookingWrite,
    bool ReservationRead,
    bool ReservationWrite,
    bool StatePoliceRead,
    bool StatePoliceWrite,
    bool StatePoliceSettings,
    bool SettingAgency,
    bool SettingUser,
    bool SettingRoomRead,
    bool SettingRoomWrite,
    bool RoomStatusUpdate,
    bool FinanceRead,
    bool FinanceWrite,
    bool RestaurantRead,
    bool RestaurantWrite);
