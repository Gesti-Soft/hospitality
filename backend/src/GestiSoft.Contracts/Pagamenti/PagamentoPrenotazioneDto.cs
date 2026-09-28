using GestiSoft.Domain.Enums;

namespace GestiSoft.Contracts.Pagamenti;

/// <summary>Un incasso o un rimborso su una prenotazione. Importo sempre positivo: il rimborso si sottrae.</summary>
public record PagamentoPrenotazioneDto(
    Guid Id,
    DateOnly Data,
    decimal Importo,
    TipoPagamento Tipo,
    ModalitaPagamento? Metodo,
    string? Nota,
    string? RegistratoDa,
    DateTime RegistratoIlUtc);

public record SalvaPagamentoRequest(DateOnly Data, decimal Importo, TipoPagamento Tipo, ModalitaPagamento? Metodo, string? Nota);
