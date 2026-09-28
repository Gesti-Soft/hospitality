using GestiSoft.Domain.Common;
using GestiSoft.Domain.Enums;

namespace GestiSoft.Domain.Entities;

/// <summary>
/// Un incasso (o un rimborso) su una prenotazione: acconto alla prenotazione, caparra, saldo al
/// check-out. Prenotazione.ImportoPagato è la loro somma, tenuta aggiornata da PagamentiService.
/// Nella Cassa conta la data del pagamento, non l'anno del soggiorno: un acconto di dicembre per un
/// soggiorno di gennaio è entrato a dicembre. Mai dati della carta: solo il metodo.
/// </summary>
public class PagamentoPrenotazione : TenantEntity
{
    public Guid PrenotazioneId { get; set; }

    /// <summary>Data civile dell'incasso, senza fuso orario.</summary>
    public DateOnly Data { get; set; }

    /// <summary>Sempre positivo: il segno lo dà il tipo (il rimborso si sottrae).</summary>
    public decimal Importo { get; set; }

    public TipoPagamento Tipo { get; set; } = TipoPagamento.Acconto;

    /// <summary>Null solo per gli importi registrati prima di questo registro, di cui il metodo non si sa.</summary>
    public ModalitaPagamento? Metodo { get; set; }

    public string? Nota { get; set; }

    /// <summary>Chi l'ha registrato (l'email, come l'operatore nel log).</summary>
    public string? RegistratoDa { get; set; }
}
