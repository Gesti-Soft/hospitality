namespace GestiSoft.Domain.Enums;

public enum TipoNotifica
{
    NuovaPrenotazione = 1,
    PrenotazioneAnnullata = 2,
    PrenotazioneModificata = 3,
    LicenzaInScadenza = 4,
    LicenzaScaduta = 5,
    SchedineInviate = 6,
    CheckOutDimenticato = 7,

    /// <summary>Schedina alloggiati non trasmessa entro il termine di legge: l'invio automatico non la prende più, va registrata a mano sul portale della Polizia di Stato (vedi TerminiSchedina).</summary>
    SchedinaFuoriTermine = 8,
}
