using GestiSoft.Domain.Enums;

namespace GestiSoft.Contracts.Notifiche;

public record NotificaDto(
    Guid Id,
    TipoNotifica Tipo,
    string Titolo,
    string Messaggio,
    Guid? PrenotazioneId,
    string? Canale,
    DateTime CreatedAtUtc,
    DateTime? LettaAtUtc);
