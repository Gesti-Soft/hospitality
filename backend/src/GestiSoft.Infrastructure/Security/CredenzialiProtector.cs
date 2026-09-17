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

    public CredenzialiProtector(byte[] chiave)
    {
        if (chiave.Length != LunghezzaChiave)
        {
            throw new InvalidOperationException($"Chiave di cifratura credenziali non valida: servono {LunghezzaChiave} byte, ne sono arrivati {chiave.Length}.");
        }

        _chiave = chiave;
    }

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

        byte[] chiave;
        try
        {
            chiave = Convert.FromBase64String(valore.Trim());
        }
        catch (FormatException)
        {
            throw new InvalidOperationException("Credenziali:ChiaveCifratura non è in base64 valido.");
        }

        return new CredenzialiProtector(chiave);
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

        var nonce = pacchetto.AsSpan(0, LunghezzaNonce);
        var tag = pacchetto.AsSpan(LunghezzaNonce, LunghezzaTag);
        var cifrato = pacchetto.AsSpan(LunghezzaNonce + LunghezzaTag);
        var chiaro = new byte[cifrato.Length];

        using var aes = new AesGcm(_chiave, LunghezzaTag);
        try
        {
            aes.Decrypt(nonce, cifrato, tag, chiaro);
        }
        catch (CryptographicException ex)
        {
            // Quasi sempre significa chiave diversa da quella con cui il valore è stato scritto:
            // meglio fermarsi che restituire in silenzio una credenziale sbagliata, che si
            // tradurrebbe in inviti al portale rifiutati senza una ragione comprensibile.
            throw new InvalidOperationException("Credenziale cifrata illeggibile: chiave di cifratura diversa da quella usata per salvarla.", ex);
        }

        return System.Text.Encoding.UTF8.GetString(chiaro);
    }
}
