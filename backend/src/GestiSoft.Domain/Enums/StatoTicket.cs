namespace GestiSoft.Domain.Enums;

public enum StatoTicket
{
    Aperto = 1,

    /// <summary>
    /// Chiuso dal Super Admin: non accetta più messaggi e le foto allegate sono già state cancellate
    /// dal disco. Per lo stesso problema il Cliente apre un ticket nuovo.
    /// </summary>
    Chiuso = 2,
}
