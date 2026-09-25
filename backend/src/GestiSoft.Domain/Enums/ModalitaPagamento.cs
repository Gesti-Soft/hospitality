namespace GestiSoft.Domain.Enums;

/// <summary>
/// Come l'ospite ha pagato il soggiorno, stampato sulla ricevuta di locazione breve: i modelli in
/// uso lo indicano, ed è il dato che serve quando il pagamento in contanti va documentato.
/// </summary>
public enum ModalitaPagamento
{
    Contanti = 1,
    Bonifico = 2,
    CartaDiPagamento = 3,
    Assegno = 4,

    /// <summary>Incassato dal portale di prenotazione (OTA) e girato al locatore.</summary>
    PortaleOnline = 5,
}
