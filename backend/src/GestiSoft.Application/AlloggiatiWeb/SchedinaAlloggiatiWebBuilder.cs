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

    /// <summary>Larghezze delle colonne che contengono testo libero, per non scriverci dentro più di quanto ci stia.</summary>
    public const int LarghezzaCognome = 50;
    public const int LarghezzaNome = 30;
    public const int LarghezzaNumeroDocumento = 20;

    /// <summary>
    /// Motivi per cui questa schedina non è spedibile, vuoto se è a posto. Controlla le stesse cose
    /// che <see cref="Costruisci"/> andrebbe a scrivere, con gli stessi elenchi di anagrafica: un
    /// campo obbligatorio che qui non si risolve finirebbe nel tracciato come una colonna di spazi,
    /// e il portale rifiuterebbe la riga senza dire quale dato mancava.
    ///
    /// Si segnala solo ciò che è **certamente** sbagliato — dato assente, codice non trovato in
    /// anagrafica, testo più lungo della colonna. Nel dubbio si lascia passare: bloccare una
    /// schedina che il portale avrebbe accettato significa non assolvere un obbligo di legge, che è
    /// peggio di un invio rifiutato.
    /// </summary>
    public IReadOnlyList<string> Valida(Ospite ospite)
    {
        var motivi = new List<string>();
        var chi = NomeLeggibile(ospite.Cognome, ospite.Nome);

        if (ospite.Prenotazione?.CheckIn is null)
        {
            motivi.Add("manca la data di arrivo sulla prenotazione");
        }

        if ((ospite.Permanenza ?? 0) < 1)
        {
            motivi.Add("giorni di permanenza non indicati");
        }

        var codiceTipoAlloggiato = CodiceTipoAlloggiato(ospite.TipoOspite).Trim();
        if (codiceTipoAlloggiato.Length == 0)
        {
            motivi.Add($"{chi}: tipo alloggiato mancante o non riconosciuto{Valore(ospite.TipoOspite)}");
        }

        motivi.AddRange(ValidaPersona(chi, ospite.Cognome, ospite.Nome, ospite.Sesso, ospite.DataNascita, ospite.StatoNascita, ospite.LuogoNascita, ospite.Cittadinanza));

        // I familiari e i membri di un gruppo non portano documento (tipi 19 e 20): per tutti gli
        // altri è obbligatorio, ed è il blocco di campi che più spesso resta da compilare.
        if (codiceTipoAlloggiato.Length > 0 && codiceTipoAlloggiato is not ("19" or "20"))
        {
            if (_documenti[ospite.Documento ?? string.Empty].FirstOrDefault() is null)
            {
                motivi.Add($"{chi}: tipo documento mancante o non riconosciuto{Valore(ospite.Documento)}");
            }

            if (string.IsNullOrWhiteSpace(ospite.NumeroDocumento))
            {
                motivi.Add($"{chi}: manca il numero del documento");
            }
            else if (TestoTracciato.EccedeLarghezza(ospite.NumeroDocumento, LarghezzaNumeroDocumento))
            {
                motivi.Add($"{chi}: numero documento oltre {LarghezzaNumeroDocumento} caratteri");
            }

            if (_luoghi[ExtractCity(ospite.RilascioDocumento) ?? string.Empty].FirstOrDefault() is null)
            {
                motivi.Add($"{chi}: luogo di rilascio del documento mancante o non riconosciuto{Valore(ospite.RilascioDocumento)}");
            }
        }

        foreach (var membro in ospite.Membri)
        {
            motivi.AddRange(ValidaPersona(
                NomeLeggibile(membro.Cognome, membro.Nome),
                membro.Cognome, membro.Nome, membro.Sesso, membro.DataNascita, membro.StatoNascita, membro.LuogoNascita, membro.Cittadinanza));
        }

        return motivi;
    }

    /// <summary>Campi richiesti a chiunque compaia nella schedina, capofamiglia o membro che sia.</summary>
    private List<string> ValidaPersona(
        string chi, string? cognome, string? nome, Sesso? sesso, DateTime? dataNascita,
        string? statoNascita, string? luogoNascita, string? cittadinanza)
    {
        var motivi = new List<string>();

        if (string.IsNullOrWhiteSpace(cognome))
        {
            motivi.Add($"{chi}: manca il cognome");
        }
        else if (TestoTracciato.EccedeLarghezza(cognome, LarghezzaCognome))
        {
            motivi.Add($"{chi}: cognome oltre {LarghezzaCognome} caratteri");
        }

        if (string.IsNullOrWhiteSpace(nome))
        {
            motivi.Add($"{chi}: manca il nome");
        }
        else if (TestoTracciato.EccedeLarghezza(nome, LarghezzaNome))
        {
            motivi.Add($"{chi}: nome oltre {LarghezzaNome} caratteri");
        }

        if (sesso is null)
        {
            motivi.Add($"{chi}: manca il sesso");
        }

        if (dataNascita is null)
        {
            motivi.Add($"{chi}: manca la data di nascita");
        }

        if (_luoghi[statoNascita ?? string.Empty].FirstOrDefault() is null)
        {
            motivi.Add($"{chi}: stato di nascita mancante o non riconosciuto{Valore(statoNascita)}");
        }
        else if (string.Equals(statoNascita, "ITALIA", StringComparison.OrdinalIgnoreCase)
                 && _luoghi[ExtractCity(luogoNascita) ?? string.Empty].FirstOrDefault() is null)
        {
            // Solo per chi è nato in Italia: per gli altri il comune non si trasmette affatto.
            motivi.Add($"{chi}: comune di nascita mancante o non riconosciuto{Valore(luogoNascita)}");
        }

        if (_luoghi[cittadinanza ?? string.Empty].FirstOrDefault() is null)
        {
            motivi.Add($"{chi}: cittadinanza mancante o non riconosciuta{Valore(cittadinanza)}");
        }

        return motivi;
    }

    private static string NomeLeggibile(string? cognome, string? nome)
    {
        var completo = $"{cognome} {nome}".Trim();
        return completo.Length == 0 ? "Ospite senza nome" : completo;
    }

    /// <summary>Il valore rifiutato, tra virgolette, per far capire subito cosa correggere — niente se il campo è proprio vuoto.</summary>
    private static string Valore(string? valore) =>
        string.IsNullOrWhiteSpace(valore) ? string.Empty : $" (\"{valore.Trim()}\")";

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

    /// <summary>
    /// Scrive un testo libero nella sua colonna, ripulito (vedi <see cref="TestoTracciato"/>) e
    /// tagliato se eccede. Il taglio è l'ultima rete di sicurezza, non il comportamento previsto:
    /// <see cref="Valida"/> intercetta prima le schedine troppo lunghe e le tiene fuori dall'invio,
    /// perché un cognome mozzato è comunque un dato sbagliato trasmesso a una PA. Ma se qualcosa
    /// sfugge, meglio un campo tagliato che una riga disallineata: nel tracciato a posizioni fisse
    /// un carattere di troppo sposta tutti i campi successivi e fa rifiutare l'intero record.
    /// </summary>
    private static string Pad(string? value, int width) => Tronca(TestoTracciato.Normalizza(value), width).PadRight(width);

    /// <summary>Come sopra per i codici presi dall'anagrafica, che non vanno ripuliti: sono già codici.</summary>
    private static string PadCode(string? code, int width) => Tronca(code ?? string.Empty, width).PadRight(width);

    private static string Tronca(string valore, int width) => valore.Length > width ? valore[..width] : valore;

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
