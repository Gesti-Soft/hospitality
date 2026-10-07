using System.Text;

namespace GestiSoft.Application.Fatturazione;

/// <summary>
/// I campi testo della FatturaPA ammettono solo Basic Latin e Latin-1 Supplement (U+0020-007E,
/// U+00A0-00FF): un trattino lungo, una virgoletta tipografica o il simbolo dell'euro fanno
/// scartare il file. Si traslittera quello che ha un equivalente, si toglie il resto, poi si taglia
/// alla lunghezza massima del campo (dopo, perché la traslitterazione può allungare il testo).
/// </summary>
public static class TestoFatturaPA
{
    public const int Denominazione = 80;
    public const int NomeCognome = 60;
    public const int Indirizzo = 60;
    public const int Comune = 60;
    public const int NumeroCivico = 8;
    public const int Descrizione = 1000;
    public const int RiferimentoNormativo = 100;

    /// <summary>Null o vuoto restano null: l'elemento non va scritto, non scritto vuoto.</summary>
    public static string? Pulisci(string? testo, int lunghezzaMassima)
    {
        if (string.IsNullOrWhiteSpace(testo))
        {
            return null;
        }

        var sb = new StringBuilder(testo.Length);
        foreach (var c in testo.Normalize(NormalizationForm.FormC))
        {
            switch (c)
            {
                case '\u2013' or '\u2014' or '\u2212' or '\u2010' or '\u2011':
                    sb.Append('-');
                    break;
                case '\u2018' or '\u2019' or '\u201A' or '\u2032':
                    sb.Append('\'');
                    break;
                case '\u201C' or '\u201D' or '\u201E' or '\u2033':
                    sb.Append('"');
                    break;
                case '\u2026':
                    sb.Append("...");
                    break;
                case '\u20AC':
                    sb.Append("EUR");
                    break;
                // Lettere senza scomposizione: la normalizzazione non trova la lettera base.
                case '\u0141':
                    sb.Append('L');
                    break;
                case '\u0110':
                    sb.Append('D');
                    break;
                case '\u0142':
                    sb.Append('l');
                    break;
                case '\u0111':
                    sb.Append('d');
                    break;
                case '\r' or '\n' or '\t':
                    sb.Append(' ');
                    break;
                case '\u00A0':
                    sb.Append(' ');
                    break;
                case >= ' ' and <= '~':
                case >= '\u00A1' and <= '\u00FF':
                    sb.Append(c);
                    break;
                case var _ when char.IsSurrogate(c):
                    // Emoji e simboli fuori dal piano base: nessun equivalente, e la normalizzazione di
                    // mezza coppia surrogata solleverebbe un'eccezione.
                    break;
                default:
                    // Lettere accentate fuori da Latin-1 (ő, č…): si prova la lettera base prima di toglierle.
                    var basi = c.ToString().Normalize(NormalizationForm.FormD)
                        .Where(b => b is >= ' ' and <= '~');
                    sb.Append(string.Concat(basi));
                    break;
            }
        }

        // Gli spazi doppi nati dagli a capo non servono, e il tracciato collassa comunque gli spazi.
        var pulito = string.Join(' ', sb.ToString().Split(' ', StringSplitOptions.RemoveEmptyEntries));
        if (pulito.Length > lunghezzaMassima)
        {
            pulito = pulito[..lunghezzaMassima].TrimEnd();
        }

        return pulito.Length == 0 ? null : pulito;
    }
}
