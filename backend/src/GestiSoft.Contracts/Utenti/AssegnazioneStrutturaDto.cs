using GestiSoft.Domain.Enums;

namespace GestiSoft.Contracts.Utenti;

/// <summary>Come UtenteStrutturaDto, con i dati dell'Utente inclusi — per l'elenco della schermata Utenti (Fase 9), che non deve fare una chiamata per riga per mostrare email/nome.</summary>
public record AssegnazioneStrutturaDto(
    Guid Id,
    Guid UtenteId,
    string Email,
    string? Nome,
    string? Cognome,
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
