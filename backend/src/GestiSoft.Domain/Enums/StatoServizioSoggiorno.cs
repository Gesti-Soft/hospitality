namespace GestiSoft.Domain.Enums;

/// <summary>A che punto è un servizio durante il soggiorno (pulizia o cambio biancheria) per una camera occupata.</summary>
public enum StatoServizioSoggiorno
{
    /// <summary>Nessuna frequenza impostata, oppure la prossima cadrebbe il giorno della partenza o dopo: lì c'è la pulizia del check-out.</summary>
    NonPrevisto = 1,

    /// <summary>Previsto in un giorno successivo di questo soggiorno.</summary>
    Programmato = 2,

    DaFareOggi = 3,

    /// <summary>Previsto in un giorno passato e non ancora segnato come fatto.</summary>
    InRitardo = 4,

    /// <summary>L'ospite ci ha rinunciato.</summary>
    Rinunciato = 5,
}
