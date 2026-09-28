using GestiSoft.Domain.Common;

namespace GestiSoft.Domain.Entities;

/// <summary>
/// Le prenotazioni fatturate da un documento: di norma una, più d'una quando una sola persona paga
/// per tutti (due famiglie, un'azienda con più camere). La prima è quella di chi paga, e resta anche
/// in DatiFattura.PrenotazioneId.
/// </summary>
public class FatturaPrenotazione : TenantEntity
{
    public Guid DatiFatturaId { get; set; }

    public Guid PrenotazioneId { get; set; }
}
