namespace GestiSoft.Domain.Entities;

/// <summary>
/// Tabella ponte: tipologie camera instradate su una data <see cref="PayTouristStruttura"/>.
/// Sostituisce la stringa CSV di id (<c>PayTouristUser.IdTipology</c>) del legacy — stessa scelta
/// già fatta per <c>OsservatorioAppartamentoTipologia</c> in Fase 7.
/// </summary>
public class PayTouristStrutturaTipologia
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public Guid PayTouristStrutturaId { get; set; }

    public PayTouristStruttura? PayTouristStruttura { get; set; }

    public Guid TipologiaId { get; set; }

    public SettingTipologia? Tipologia { get; set; }
}
