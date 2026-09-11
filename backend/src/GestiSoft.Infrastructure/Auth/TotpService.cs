using System.Security.Cryptography;
using GestiSoft.Application.Auth;
using OtpNet;

namespace GestiSoft.Infrastructure.Auth;

/// <summary>
/// Implementazione TOTP sopra Otp.NET — parametri lasciati ai default dello standard (SHA-1,
/// 6 cifre, finestra da 30 secondi) perché sono quelli che Google Authenticator si aspetta
/// leggendo un QR: cambiarli richiederebbe all'utente di configurare l'app a mano.
/// </summary>
public class TotpService : ITotpService
{
    private const string Emittente = "GestiSoft";

    /// <summary>160 bit, la dimensione raccomandata dall'RFC 4226 per la chiave HMAC-SHA1.</summary>
    public string GeneraSecret() => Base32Encoding.ToString(RandomNumberGenerator.GetBytes(20));

    public string CostruisciUriOtpauth(string secret, string etichetta)
    {
        var etichettaCodificata = Uri.EscapeDataString($"{Emittente}:{etichetta}");
        return $"otpauth://totp/{etichettaCodificata}?secret={secret}&issuer={Uri.EscapeDataString(Emittente)}&algorithm=SHA1&digits=6&period=30";
    }

    public bool VerificaCodice(string secret, string codice)
    {
        var codiceNormalizzato = new string(codice.Where(char.IsDigit).ToArray());
        if (codiceNormalizzato.Length != 6)
        {
            return false;
        }

        try
        {
            var totp = new Totp(Base32Encoding.ToBytes(secret));
            return totp.VerifyTotp(codiceNormalizzato, out _, new VerificationWindow(previous: 1, future: 1));
        }
        catch (ArgumentException)
        {
            // Segreto non decodificabile (riga manomessa a mano nel database): nessun codice è valido.
            return false;
        }
    }
}
