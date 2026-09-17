using System.Security.Cryptography;
using Microsoft.Extensions.Configuration;

namespace GestiSoft.Infrastructure.Security;

/// <summary>
/// Cifra a riposo le credenziali dei servizi esterni (token PayTourist, password e Ws Key di
/// Alloggiati Web, password Osservatorio, token OTA globale): valori che aprono l'accesso a portali
/// della Pubblica Amministrazione e che finora stavano in chiaro nel database, quindi anche in ogni
/// copia di backup e in ogni dump scaricato per assistenza.
/// <para>
/// La chiave sta <b>fuori</b> dal database (<c>Credenziali:ChiaveCifratura</c>, variabile d'ambiente
/// <c>Credenziali__ChiaveCifratura</c>, 32 byte in base64): è il punto della cosa, perché il rischio
/// concreto è che giri un dump. Tenere le chiavi in una tabella dello stesso database non
/// proteggerebbe da quello scenario.
/// </para>
/// <para>
/// AES-GCM con nonce casuale ad ogni scrittura: lo stesso token cifrato due volte dà due valori
/// diversi, e il tag di autenticazione fa fallire la lettura se qualcuno ha manomesso il dato.
/// </para>
/// </summary>
public sealed class CredenzialiProtector
{
    /// <summary>Marca i valori cifrati. Quelli senza prefisso sono i valori storici, scritti in chiaro prima di questa modifica: si leggono così come sono e vengono cifrati alla prima riscrittura.</summary>
    public const string Prefisso = "enc:v1:";

    private const int LunghezzaNonce = 12;
    private const int LunghezzaTag = 16;
    private const int LunghezzaChiave = 32;

    private readonly byte[] _chiave;
    private readonly byte[]? _chiavePrecedente;

    /// <param name="chiavePrecedente">
    /// Chiave usata prima di una rotazione: serve solo a <b>leggere</b> ciò che non è ancora stato
    /// riscritto. La scrittura usa sempre e soltanto la chiave corrente, altrimenti la rotazione non
    /// finirebbe mai.
    /// </param>
    public CredenzialiProtector(byte[] chiave, byte[]? chiavePrecedente = null)
    {
        if (chiave.Length != LunghezzaChiave)
        {
            throw new InvalidOperationException($"Chiave di cifratura credenziali non valida: servono {LunghezzaChiave} byte, ne sono arrivati {chiave.Length}.");
        }

        if (chiavePrecedente is not null && chiavePrecedente.Length != LunghezzaChiave)
        {
            throw new InvalidOperationException($"Chiave di cifratura precedente non valida: servono {LunghezzaChiave} byte, ne sono arrivati {chiavePrecedente.Length}.");
        }

        _chiave = chiave;
        _chiavePrecedente = chiavePrecedente;
    }

    /// <summary>Vero durante una rotazione: c'è una chiave vecchia da cui migrare, e le credenziali vanno riscritte tutte con quella corrente.</summary>
    public bool RotazioneInCorso => _chiavePrecedente is not null;

    /// <summary>
    /// Legge la chiave dalla configurazione. Assente o malformata è un errore di avvio, non un
    /// avviso: partire senza cifratura significherebbe riscrivere in chiaro, alla prima modifica,
    /// credenziali che si credono protette.
    /// </summary>
    public static CredenzialiProtector Da(IConfiguration configuration)
    {
        var valore = configuration["Credenziali:ChiaveCifratura"];
        if (string.IsNullOrWhiteSpace(valore))
        {
            throw new InvalidOperationException(
                "Credenziali:ChiaveCifratura non configurata (env Credenziali__ChiaveCifratura). " +
                "Generare 32 byte casuali in base64, per esempio con: openssl rand -base64 32");
        }

        return new CredenzialiProtector(
            DaBase64(valore, "Credenziali:ChiaveCifratura")!,
            DaBase64(configuration["Credenziali:ChiaveCifraturaPrecedente"], "Credenziali:ChiaveCifraturaPrecedente"));
    }

    private static byte[]? DaBase64(string? valore, string nome)
    {
        if (string.IsNullOrWhiteSpace(valore))
        {
            return null;
        }

        try
        {
            return Convert.FromBase64String(valore.Trim());
        }
        catch (FormatException)
        {
            throw new InvalidOperationException($"{nome} non è in base64 valido.");
        }
    }

    public static bool IsProtetto(string? valore) =>
        valore is not null && valore.StartsWith(Prefisso, StringComparison.Ordinal);

    public string? Proteggi(string? valore)
    {
        if (string.IsNullOrEmpty(valore) || IsProtetto(valore))
        {
            return valore;
        }

        var chiaro = System.Text.Encoding.UTF8.GetBytes(valore);
        var nonce = RandomNumberGenerator.GetBytes(LunghezzaNonce);
        var tag = new byte[LunghezzaTag];
        var cifrato = new byte[chiaro.Length];

        using var aes = new AesGcm(_chiave, LunghezzaTag);
        aes.Encrypt(nonce, chiaro, cifrato, tag);

        var pacchetto = new byte[LunghezzaNonce + LunghezzaTag + cifrato.Length];
        nonce.CopyTo(pacchetto, 0);
        tag.CopyTo(pacchetto, LunghezzaNonce);
        cifrato.CopyTo(pacchetto, LunghezzaNonce + LunghezzaTag);

        return Prefisso + Convert.ToBase64String(pacchetto);
    }

    public string? Leggi(string? valore)
    {
        // Valore storico, scritto prima che la cifratura esistesse: si restituisce com'è, altrimenti
        // ogni struttura già configurata smetterebbe di funzionare al primo deploy.
        if (string.IsNullOrEmpty(valore) || !IsProtetto(valore))
        {
            return valore;
        }

        var pacchetto = Convert.FromBase64String(valore[Prefisso.Length..]);
        if (pacchetto.Length < LunghezzaNonce + LunghezzaTag)
        {
            throw new InvalidOperationException("Credenziale cifrata illeggibile: pacchetto più corto del previsto.");
        }

        if (Decifra(pacchetto, _chiave) is { } conCorrente)
        {
            return conCorrente;
        }

        // Rotazione in corso: questo valore è ancora scritto con la chiave vecchia e non è stato
        // ancora riscritto. La lettura funziona lo stesso, la riscrittura la fa il seeder all'avvio.
        if (_chiavePrecedente is not null && Decifra(pacchetto, _chiavePrecedente) is { } conPrecedente)
        {
            return conPrecedente;
        }

        // Meglio fermarsi che restituire in silenzio una credenziale sbagliata, che si tradurrebbe
        // in invii al portale rifiutati senza una ragione comprensibile.
        throw new InvalidOperationException("Credenziale cifrata illeggibile: chiave di cifratura diversa da quella usata per salvarla.");
    }

    private static string? Decifra(byte[] pacchetto, byte[] chiave)
    {
        var nonce = pacchetto.AsSpan(0, LunghezzaNonce);
        var tag = pacchetto.AsSpan(LunghezzaNonce, LunghezzaTag);
        var cifrato = pacchetto.AsSpan(LunghezzaNonce + LunghezzaTag);
        var chiaro = new byte[cifrato.Length];

        using var aes = new AesGcm(chiave, LunghezzaTag);
        try
        {
            aes.Decrypt(nonce, cifrato, tag, chiaro);
        }
        catch (CryptographicException)
        {
            return null;
        }

        return System.Text.Encoding.UTF8.GetString(chiaro);
    }
}
