using GestiSoft.Domain.Enums;

namespace GestiSoft.Contracts.Assistenza;

public record TicketDto(
    Guid Id,
    int Numero,
    Guid StrutturaId,
    string StrutturaNome,
    string ClienteRagioneSociale,
    string Oggetto,
    StatoTicket Stato,
    DateTime CreatedAtUtc,
    DateTime UltimoMessaggioAtUtc,
    bool UltimoMessaggioDaStaff,
    // Calcolato su chi chiede: per la struttura c'è una risposta dello staff da leggere, per lo staff un messaggio della struttura.
    bool NonLetto,
    DateTime? ChiusoAtUtc,
    DateTime? AnonimizzatoAtUtc);

public record TicketDettaglioDto(TicketDto Ticket, IReadOnlyList<TicketMessaggioDto> Messaggi);

public record TicketMessaggioDto(
    Guid Id,
    bool DaStaff,
    string Autore,
    string Testo,
    DateTime CreatedAtUtc,
    IReadOnlyList<TicketAllegatoDto> Allegati);

public record TicketAllegatoDto(Guid Id, string NomeFile, long DimensioneByte, bool Eliminato);
