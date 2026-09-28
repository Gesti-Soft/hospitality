namespace GestiSoft.Domain.Enums;

/// <summary>
/// Che cosa rappresenta un pagamento registrato su una prenotazione. Il rimborso è denaro
/// restituito all'ospite e si sottrae dal pagato; gli altri si sommano.
/// </summary>
public enum TipoPagamento
{
    Acconto = 1,
    Caparra = 2,
    Saldo = 3,
    Altro = 4,
    Rimborso = 5,
}
