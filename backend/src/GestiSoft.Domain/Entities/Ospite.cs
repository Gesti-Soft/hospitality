using GestiSoft.Domain.Common;
using GestiSoft.Domain.Enums;

namespace GestiSoft.Domain.Entities;

/// <summary>
/// Testata ospite/gruppo collegata a una prenotazione. Porta 1:1 da
/// OrderManagement.Model.BusinesObject.Ospiti del sistema legacy (typo "Luoogo"/"EsenteDaTAssa"
/// corretti deliberatamente: LuogoNascita, EsenteDaTassa).
/// </summary>
public class Ospite : TenantEntity
{
    public string? TipoOspite { get; set; }

    public Guid? PrenotazioneId { get; set; }

    public Prenotazione? Prenotazione { get; set; }

    public int? Permanenza { get; set; }

    public DateTime? DataNascita { get; set; }

    public Sesso? Sesso { get; set; }

    public string? Cognome { get; set; }

    public string? Nome { get; set; }

    public string? Cittadinanza { get; set; }

    public string? LuogoNascita { get; set; }

    public string? StatoNascita { get; set; }

    public string? LuogoResidenza { get; set; }

    public string? Email { get; set; }

    public string? Documento { get; set; }

    public string? NumeroDocumento { get; set; }

    public string? RilascioDocumento { get; set; }

    public bool EsenteDaTassa { get; set; }

    public ICollection<OspiteRiga> Membri { get; set; } = new List<OspiteRiga>();
}
