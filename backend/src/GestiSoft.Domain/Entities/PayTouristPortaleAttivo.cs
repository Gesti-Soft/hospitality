namespace GestiSoft.Domain.Entities;

/// <summary>
/// Un portale online per il quale l'imposta di soggiorno è incassata dal portale stesso, e non
/// dalla struttura. Ne esiste una riga per ogni portale che l'operatore ha spuntato in Impostazioni.
/// <para>
/// Non basta sapere che il Comune riconosce un portale: la stessa struttura può avere l'incasso
/// automatico attivo su un canale e non su un altro (Airbnb riscuote d'ufficio nei Comuni
/// convenzionati, Booking solo dove ha un accordo attivo per quella struttura). Dichiarare come
/// "incassata dal portale" una prenotazione il cui denaro è invece passato dalla cassa della
/// struttura significa che il Comune aspetta quei soldi da qualcuno che non glieli manderà.
/// </para>
/// <para>
/// <see cref="Nome"/> è il nome del portale come lo restituisce PayTourist, ed è anche la chiave
/// con cui si riconosce il canale di una prenotazione (<c>Prenotazione.Agenzia</c>): va conservato
/// com'è, non normalizzato.
/// </para>
/// </summary>
public class PayTouristPortaleAttivo
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public Guid PayTouristIntegrazioneId { get; set; }

    public PayTouristIntegrazione? Integrazione { get; set; }

    /// <summary>Id del portale su PayTourist (<c>online_portal</c> nell'invio).</summary>
    public int IdPortale { get; set; }

    public string Nome { get; set; } = string.Empty;
}
