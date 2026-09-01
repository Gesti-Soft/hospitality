namespace GestiSoft.Application.Logging;

/// <summary>
/// Un Cliente (utente di struttura, non Super Admin) vede solo un sottoinsieme dei log — non
/// errori generici, sincronizzazioni Wubook, login/logout o azioni interne del Super Admin, che
/// restano riservati allo staff GestiSoft (vedi LogController). Le categorie qui elencate sono le
/// uniche pensate per essere lette da chi gestisce la struttura: modifiche agli utenti, gestione
/// prenotazioni, servizi esterni attivati/disattivati dal Super Admin, ed esito (riuscito o no,
/// col conteggio) degli invii automatici delle schedine.
/// </summary>
public static class LogVisibilita
{
    public static readonly IReadOnlyList<string> CategorieVisibiliCliente =
    [
        "Utente",
        "Prenotazione",
        "Servizi",
        "AlloggiatiWeb",
        "Osservatorio",
        "PayTourist",
    ];
}
