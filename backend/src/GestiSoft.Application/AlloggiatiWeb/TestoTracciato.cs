using System.Globalization;
using System.Text;

namespace GestiSoft.Application.AlloggiatiWeb;

/// <summary>
/// Ripulisce i campi liberi (cognome, nome, numero documento) prima di scriverli nel tracciato a
/// posizioni fisse di Alloggiati Web.
///
/// Serve perché il tracciato non perdona: i campi non sono separati da nulla, stanno in colonne di
/// larghezza data, e un valore più lungo del previsto fa slittare tutti quelli dopo di lui —
/// il portale rifiuta l'intera riga senza che si capisca perché. Un "a capo" finito dentro un
/// cognome spezza addirittura il record in due.
///
/// Si corregge solo ciò che è sicuro correggere: spazi di troppo, caratteri di controllo, accenti
/// latini (È → E), che il tracciato non prevede. **Non** si toccano gli alfabeti non latini: senza
/// la certezza che il portale li rifiuti, cancellarli renderebbe illeggibile il nome di un ospite
/// straniero, che è peggio del problema che si vuole evitare.
/// </summary>
public static class TestoTracciato
{
    /// <summary>
    /// Valore ripulito: niente spazi ai bordi o doppi in mezzo, niente caratteri di controllo (un
    /// "a capo" diventa uno spazio, non sparisce), accenti latini ridotti alla lettera base.
    /// </summary>
    public static string Normalizza(string? valore)
    {
        if (string.IsNullOrWhiteSpace(valore))
        {
            return string.Empty;
        }

        var senzaControlli = new StringBuilder(valore.Length);
        foreach (var carattere in valore)
        {
            senzaControlli.Append(char.IsControl(carattere) ? ' ' : carattere);
        }

        var senzaAccenti = new StringBuilder(senzaControlli.Length);
        foreach (var carattere in senzaControlli.ToString().Normalize(NormalizationForm.FormD))
        {
            // Solo i segni diacritici sopra una lettera latina: quelli spariscono lasciando la
            // lettera. Un carattere di un altro alfabeto non è un diacritico e resta com'è.
            if (CharUnicodeInfo.GetUnicodeCategory(carattere) != UnicodeCategory.NonSpacingMark)
            {
                senzaAccenti.Append(carattere);
            }
        }

        return string.Join(' ', senzaAccenti.ToString().Normalize(NormalizationForm.FormC)
            .Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries));
    }

    /// <summary>
    /// Il valore normalizzato supera la colonna che lo deve contenere. Non si taglia in silenzio: un
    /// cognome mozzato è un dato sbagliato trasmesso a una PA, e va corretto da una persona.
    /// </summary>
    public static bool EccedeLarghezza(string? valore, int larghezza) => Normalizza(valore).Length > larghezza;
}
