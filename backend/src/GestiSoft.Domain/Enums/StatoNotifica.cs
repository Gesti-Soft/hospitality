namespace GestiSoft.Domain.Enums;

public enum StatoNotifica
{
    /// <summary>
    /// Cancellazione Wubook in finestra di grazia: non ancora visibile all'utente, in attesa di
    /// scoprire se entro breve arriva una nuova prenotazione corrispondente (stesso canale, stesso
    /// ospite) che la renderebbe in realtà una modifica — vedi NotificaService.
    /// </summary>
    InAttesa = 1,

    Confermata = 2,
}
