namespace GestiSoft.Domain.Entities;

/// <summary>
/// Tabella ponte: tipologie camera instradate su un dato Appartamento Osservatorio Turistico.
/// Sostituisce la stringa CSV di id (<c>Appartementi.IdAppartamento</c>) del legacy — vedi
/// <see cref="OsservatorioAppartamento"/>.
/// </summary>
public class OsservatorioAppartamentoTipologia
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public Guid OsservatorioAppartamentoId { get; set; }

    public OsservatorioAppartamento? OsservatorioAppartamento { get; set; }

    public Guid TipologiaId { get; set; }

    public SettingTipologia? Tipologia { get; set; }
}
