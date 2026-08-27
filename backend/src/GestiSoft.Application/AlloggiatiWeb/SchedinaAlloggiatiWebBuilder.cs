using GestiSoft.Domain.Entities;
using GestiSoft.Domain.Enums;

namespace GestiSoft.Application.AlloggiatiWeb;

/// <summary>
/// Costruisce le righe a tracciato fisso della schedina Alloggiati Web per un Ospite (capofamiglia)
/// e i suoi Membri — porta 1:1 la logica di StatePoliceLogic.SendSchedine del legacy (il ramo usato
/// dal batch schedulato: SendSchedina, la versione a invio singolo, aveva un bug per cui il sesso
/// dei membri veniva preso dal capofamiglia invece che dal proprio — qui viene sempre usato quello
/// corretto del ramo batch). Campi a larghezza fissa (tracciato ufficiale Alloggiati Web):
/// TipoAlloggiato(2) DataArrivo(10) GiorniPermanenza(2) Cognome(50) Nome(30) Sesso(1)
/// DataNascita(10) ComuneNascita(9) ProvinciaNascita(2) StatoNascita(9) Cittadinanza(9)
/// TipoDocumento(5) NumeroDocumento(20) LuogoRilascioDocumento(9).
/// </summary>
public class SchedinaAlloggiatiWebBuilder
{
    private readonly ILookup<string, VoceAnagrafica> _luoghi;
    private readonly ILookup<string, VoceAnagrafica> _documenti;
    private readonly ILookup<string, VoceAnagrafica> _tipiAlloggiato;

    public SchedinaAlloggiatiWebBuilder(
        IReadOnlyList<VoceAnagrafica> luoghi,
        IReadOnlyList<VoceAnagrafica> documenti,
        IReadOnlyList<VoceAnagrafica> tipiAlloggiato)
    {
        _luoghi = luoghi.ToLookup(v => v.Descrizione, StringComparer.OrdinalIgnoreCase);
        _documenti = documenti.ToLookup(v => v.Descrizione, StringComparer.OrdinalIgnoreCase);
        _tipiAlloggiato = tipiAlloggiato.ToLookup(v => v.Descrizione, StringComparer.OrdinalIgnoreCase);
    }

    /// <summary>Una riga per il capofamiglia/ospite singolo, una per ciascun Membro.</summary>
    public IReadOnlyList<string> Costruisci(Ospite ospite)
    {
        var checkIn = ospite.Prenotazione?.CheckIn;
        var permanenza = ospite.Permanenza ?? 0;

        var righe = new List<string> { RigaCapofamiglia(ospite, checkIn, permanenza) };

        // Il tipo alloggiato dei membri dipende da quello del capofamiglia (17=CAPO FAMIGLIA -> 19=FAMILIARE,
        // 18=CAPO GRUPPO -> 20=MEMBRO GRUPPO), stessa regola del legacy.
        var tipoOspiteMembro = string.Equals(ospite.TipoOspite, "CAPO FAMIGLIA", StringComparison.OrdinalIgnoreCase)
            ? "FAMILIARE"
            : "MEMBRO GRUPPO";

        foreach (var membro in ospite.Membri)
        {
            righe.Add(RigaMembro(membro, tipoOspiteMembro, checkIn, permanenza));
        }

        return righe;
    }

    private string RigaCapofamiglia(Ospite ospite, DateTime? checkIn, int permanenza)
    {
        var codiceTipoAlloggiato = CodiceTipoAlloggiato(ospite.TipoOspite);
        var (luogoNascita, provinciaNascita) = RisolviLuogoNascita(ospite.StatoNascita, ospite.LuogoNascita);
        var (documento, numeroDocumento, rilascioDocumento) = RisolviDocumento(codiceTipoAlloggiato, ospite.Documento, ospite.NumeroDocumento, ospite.RilascioDocumento);

        return
            codiceTipoAlloggiato +
            FormattaData(checkIn) +
            permanenza.ToString("00") +
            Pad(ospite.Cognome, 50) +
            Pad(ospite.Nome, 30) +
            CodiceSesso(ospite.Sesso) +
            FormattaData(ospite.DataNascita) +
            luogoNascita +
            provinciaNascita +
            CodiceLuogo(ospite.StatoNascita, 9) +
            CodiceLuogo(ospite.Cittadinanza, 9) +
            documento +
            numeroDocumento +
            rilascioDocumento;
    }

    private string RigaMembro(OspiteRiga membro, string tipoOspiteMembro, DateTime? checkIn, int permanenzaGruppo)
    {
        var codiceTipoAlloggiato = CodiceTipoAlloggiato(tipoOspiteMembro);
        var (luogoNascita, provinciaNascita) = RisolviLuogoNascita(membro.StatoNascita, membro.LuogoNascita);

        // I membri di un gruppo/famiglia non portano un documento proprio nella scheda (stessa
        // scelta del legacy e dello schema OspiteRiga di questo sistema, che non ha campi
        // Documento/NumeroDocumento/RilascioDocumento) e condividono il periodo del capofamiglia.
        return
            codiceTipoAlloggiato +
            FormattaData(checkIn) +
            permanenzaGruppo.ToString("00") +
            Pad(membro.Cognome, 50) +
            Pad(membro.Nome, 30) +
            CodiceSesso(membro.Sesso) +
            FormattaData(membro.DataNascita) +
            luogoNascita +
            provinciaNascita +
            CodiceLuogo(membro.StatoNascita, 9) +
            CodiceLuogo(membro.Cittadinanza, 9) +
            Pad(null, 5) +
            Pad(null, 20) +
            Pad(null, 9);
    }

    private (string Codice, string Provincia) RisolviLuogoNascita(string? statoNascita, string? luogoNascita)
    {
        if (!string.Equals(statoNascita, "ITALIA", StringComparison.OrdinalIgnoreCase))
        {
            return (Pad(null, 9), Pad(null, 2));
        }

        var voce = _luoghi[ExtractCity(luogoNascita) ?? string.Empty].FirstOrDefault();
        return (PadCode(voce?.Codice, 9), Pad(voce?.Provincia, 2));
    }

    private (string Documento, string NumeroDocumento, string RilascioDocumento) RisolviDocumento(
        string codiceTipoAlloggiato, string? documentoDescrizione, string? numeroDocumento, string? rilascioDescrizione)
    {
        // I tipi alloggiato 19 (FAMILIARE) e 20 (MEMBRO GRUPPO) non richiedono documento — questo
        // ramo in pratica non scatta mai per il capofamiglia (che è sempre 16/17/18), fedele al
        // legacy (ControltipologiaOspite).
        if (string.IsNullOrEmpty(codiceTipoAlloggiato) || codiceTipoAlloggiato is "19" or "20")
        {
            return (Pad(null, 5), Pad(null, 20), Pad(null, 9));
        }

        var codiceDocumento = _documenti[documentoDescrizione ?? string.Empty].FirstOrDefault()?.Codice;
        var codiceLuogoRilascio = _luoghi[ExtractCity(rilascioDescrizione) ?? string.Empty].FirstOrDefault()?.Codice;

        return (PadCode(codiceDocumento, 5), Pad(numeroDocumento, 20), PadCode(codiceLuogoRilascio, 9));
    }

    private string CodiceTipoAlloggiato(string? descrizione) =>
        PadCode(_tipiAlloggiato[descrizione ?? string.Empty].FirstOrDefault()?.Codice, 2);

    private string CodiceLuogo(string? descrizione, int width) =>
        PadCode(_luoghi[descrizione ?? string.Empty].FirstOrDefault()?.Codice, width);

    private static string FormattaData(DateTime? data) => data?.ToString("dd/MM/yyyy") ?? Pad(null, 10);

    private static string CodiceSesso(Sesso? sesso) => sesso.HasValue ? ((int)sesso.Value).ToString() : "0";

    private static string Pad(string? value, int width) => (value ?? string.Empty).PadRight(width);

    private static string PadCode(string? code, int width) => (code ?? string.Empty).PadRight(width);

    /// <summary>
    /// Porta ExtractCity del legacy: i campi luogo sono salvati come "COMUNE (PROVINCIA)", qui si
    /// isola il solo nome per il confronto con la descrizione in anagrafica.
    /// </summary>
    private static string? ExtractCity(string? input)
    {
        if (string.IsNullOrEmpty(input))
        {
            return null;
        }

        var parts = input.Split(" (");
        return parts.Length > 2 ? $"{parts[0].Trim()} ({parts[1].Trim()}" : parts[0].Trim();
    }
}
