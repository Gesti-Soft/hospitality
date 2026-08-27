using GestiSoft.Domain.Common;

namespace GestiSoft.Domain.Entities;

/// <summary>
/// Corrispondenza StayId/GuestId inviati all'Osservatorio Turistico per un ospite di una
/// Prenotazione — porta <c>SyncStatePolice.Model.Model.Osservatorio.SenToOsservatorio</c> del
/// legacy (lì popolata solo in memoria dal batch e persistita a parte per il successivo lookup al
/// checkout). Necessaria perché il checkout (stay/updatefrompms) deve riusare lo stesso StayId e
/// GuestId assegnati all'arrivo, non generarne di nuovi.
/// </summary>
public class OsservatorioInvio : TenantEntity
{
    public Guid PrenotazioneId { get; set; }

    /// <summary>Null = riga del capofamiglia/ospite singolo, valorizzato = riga di un Membro.</summary>
    public Guid? OspiteRigaId { get; set; }

    public string StayId { get; set; } = string.Empty;

    public string GuestId { get; set; } = string.Empty;

    public DateTime DataInvioUtc { get; set; }
}
