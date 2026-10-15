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
    DateTime? LettaAtUtc,
    /// <summary>La pagina che la notifica apre al clic (es. "/fatturazione"). Null se è solo informativa.</summary>
    string? Collegamento);
