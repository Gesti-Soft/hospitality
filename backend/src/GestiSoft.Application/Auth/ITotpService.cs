namespace GestiSoft.Application.Auth;

/// <summary>
/// Codici a tempo (TOTP, RFC 6238) compatibili con Google Authenticator e app equivalenti.
/// </summary>
public interface ITotpService
{
    /// <summary>Nuovo segreto casuale in Base32, da mostrare una sola volta sotto forma di QR.</summary>
    string GeneraSecret();

    /// <summary>
    /// URI <c>otpauth://</c> che l'app legge dal QR. <paramref name="etichetta"/> è quello che
    /// l'utente si troverà scritto in elenco: va bene l'email, così distingue più account.
    /// </summary>
    string CostruisciUriOtpauth(string secret, string etichetta);

    /// <summary>
    /// True se il codice è quello atteso in questo momento. Accetta anche la finestra
    /// immediatamente precedente e successiva: orologi non perfettamente sincronizzati sono la
    /// causa più comune di codici rifiutati che l'utente vede come "giusti".
    /// </summary>
    bool VerificaCodice(string secret, string codice);
}
