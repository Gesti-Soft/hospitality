using GestiSoft.Application.AlloggiatiWeb;
using GestiSoft.Domain.Entities;
using GestiSoft.Domain.Enums;

namespace GestiSoft.Application.Tests;

/// <summary>
/// Controllo delle schedine prima di spedirle. Il tracciato Alloggiati Web è a posizioni fisse e
/// non perdona: un campo obbligatorio che non si risolve parte come una colonna di spazi e il
/// portale rifiuta la riga senza dire quale dato mancava; un valore più lungo della colonna fa
/// slittare tutti i campi successivi. Intercettarlo prima dell'invio è ciò che permette di
/// correggere il dato mentre si compila, invece di scoprirlo il giorno dopo quando la schedina è
/// ormai fuori dal termine di 24 ore.
/// </summary>
public class SchedinaAlloggiatiWebValidazioneTests
{
    private static SchedinaAlloggiatiWebBuilder Builder() => new(
        luoghi:
        [
            new VoceAnagrafica("ITALIA", "100000100", null),
            new VoceAnagrafica("ROMA", "058091", "RM"),
            new VoceAnagrafica("FRANCIA", "100000254", null),
        ],
        documenti: [new VoceAnagrafica("CARTA IDENTITA", "IDENT", null)],
        tipiAlloggiato:
        [
            new VoceAnagrafica("OSPITE SINGOLO", "16", null),
            new VoceAnagrafica("CAPO FAMIGLIA", "17", null),
            new VoceAnagrafica("FAMILIARE", "19", null),
        ]);

    private static Ospite OspiteValido() => new()
    {
        Cognome = "ROSSI",
        Nome = "MARIO",
        Sesso = Sesso.Maschio,
        DataNascita = new DateTime(1980, 5, 12),
        StatoNascita = "ITALIA",
        LuogoNascita = "ROMA (RM)",
        Cittadinanza = "ITALIA",
        TipoOspite = "OSPITE SINGOLO",
        Documento = "CARTA IDENTITA",
        NumeroDocumento = "AB1234567",
        RilascioDocumento = "ROMA (RM)",
        Permanenza = 3,
        Prenotazione = new Prenotazione { CheckIn = new DateTime(2026, 9, 15) },
    };

    [Fact]
    public void Valida_SchedinaCompleta_NessunMotivo()
    {
        Assert.Empty(Builder().Valida(OspiteValido()));
    }

    /// <summary>Il caso del titolo: uno spazio di troppo non è un errore, e non deve bloccare niente.</summary>
    [Fact]
    public void Valida_SpaziInPiu_NonSonoUnProblema()
    {
        var ospite = OspiteValido();
        ospite.Nome = " MARIO ";
        ospite.Cognome = "DE  ROSSI";

        Assert.Empty(Builder().Valida(ospite));
    }

    [Fact]
    public void Valida_CampiObbligatoriMancanti_LiElencaTutti()
    {
        var ospite = OspiteValido();
        ospite.Nome = "";
        ospite.DataNascita = null;
        ospite.Sesso = null;

        var motivi = Builder().Valida(ospite);

        Assert.Contains(motivi, m => m.Contains("manca il nome"));
        Assert.Contains(motivi, m => m.Contains("manca la data di nascita"));
        Assert.Contains(motivi, m => m.Contains("manca il sesso"));
    }

    /// <summary>Un comune scritto in modo che l'anagrafica non riconosce: partirebbe come nove spazi.</summary>
    [Fact]
    public void Valida_ComuneDiNascitaNonRiconosciuto_Segnalato()
    {
        var ospite = OspiteValido();
        ospite.LuogoNascita = "ROMAA (RM)";

        var motivi = Builder().Valida(ospite);

        Assert.Contains(motivi, m => m.Contains("comune di nascita") && m.Contains("ROMAA"));
    }

    /// <summary>Chi è nato all'estero non trasmette il comune: non deve essere richiesto.</summary>
    [Fact]
    public void Valida_NatoAllEstero_ComuneNonRichiesto()
    {
        var ospite = OspiteValido();
        ospite.StatoNascita = "FRANCIA";
        ospite.LuogoNascita = "PARIGI";
        ospite.Cittadinanza = "FRANCIA";

        Assert.Empty(Builder().Valida(ospite));
    }

    [Fact]
    public void Valida_DocumentoMancante_SegnalatoSoloAChiLoDevePortare()
    {
        var ospite = OspiteValido();
        ospite.NumeroDocumento = null;
        ospite.Documento = null;

        var motivi = Builder().Valida(ospite);

        Assert.Contains(motivi, m => m.Contains("numero del documento"));
        Assert.Contains(motivi, m => m.Contains("tipo documento"));
    }

    /// <summary>I familiari (tipo 19) non portano documento proprio: richiederlo bloccherebbe schedine valide.</summary>
    [Fact]
    public void Valida_Familiare_NessunDocumentoRichiesto()
    {
        var ospite = OspiteValido();
        ospite.TipoOspite = "FAMILIARE";
        ospite.Documento = null;
        ospite.NumeroDocumento = null;
        ospite.RilascioDocumento = null;

        Assert.Empty(Builder().Valida(ospite));
    }

    [Fact]
    public void Valida_CognomeOltreLaColonna_Segnalato()
    {
        var ospite = OspiteValido();
        ospite.Cognome = new string('A', SchedinaAlloggiatiWebBuilder.LarghezzaCognome + 1);

        Assert.Contains(Builder().Valida(ospite), m => m.Contains("cognome oltre"));
    }

    [Fact]
    public void Valida_DatiDiUnMembroIncompleti_Segnalati()
    {
        var ospite = OspiteValido();
        ospite.TipoOspite = "CAPO FAMIGLIA";
        ospite.Membri.Add(new OspiteRiga { Cognome = "ROSSI", Nome = "ANNA", Sesso = Sesso.Femmina, StatoNascita = "ITALIA", LuogoNascita = "ROMA (RM)", Cittadinanza = "ITALIA" });

        Assert.Contains(Builder().Valida(ospite), m => m.Contains("ROSSI ANNA") && m.Contains("data di nascita"));
    }

    [Fact]
    public void Valida_DataDiArrivoMancante_Segnalata()
    {
        var ospite = OspiteValido();
        ospite.Prenotazione = new Prenotazione { CheckIn = null };

        Assert.Contains(Builder().Valida(ospite), m => m.Contains("data di arrivo"));
    }

    /// <summary>Il tracciato è a posizioni fisse: ogni riga deve restare lunga esattamente 168 caratteri.</summary>
    [Fact]
    public void Costruisci_RigaDellaLunghezzaEsatta_ArotondoASpazi()
    {
        var riga = Assert.Single(Builder().Costruisci(OspiteValido()));

        Assert.Equal(168, riga.Length);
    }

    /// <summary>
    /// Rete di sicurezza: un valore più lungo della colonna viene tagliato invece di far slittare
    /// tutti i campi successivi — la riga resta leggibile dal portale.
    /// </summary>
    [Fact]
    public void Costruisci_ValoreTroppoLungo_NonDisallineaLaRiga()
    {
        var ospite = OspiteValido();
        ospite.Cognome = new string('A', 80);

        var riga = Assert.Single(Builder().Costruisci(ospite));

        Assert.Equal(168, riga.Length);
    }

    /// <summary>Un "a capo" dentro un campo spezzerebbe il record in due: diventa uno spazio.</summary>
    [Fact]
    public void Costruisci_ACapoDentroUnCampo_NonSpezzaIlRecord()
    {
        var ospite = OspiteValido();
        ospite.Cognome = "DE\r\nROSSI";

        var riga = Assert.Single(Builder().Costruisci(ospite));

        Assert.Equal(168, riga.Length);
        Assert.DoesNotContain('\n', riga);
        Assert.Contains("DE ROSSI", riga);
    }

    [Theory]
    [InlineData("  MARIO  ", "MARIO")]
    [InlineData("DE  ROSSI", "DE ROSSI")]
    [InlineData("PERÙ", "PERU")]
    [InlineData("D'ANGELÒ", "D'ANGELO")]
    [InlineData(null, "")]
    public void Normalizza_RipulisceIlRipulibile(string? valore, string atteso)
    {
        Assert.Equal(atteso, TestoTracciato.Normalizza(valore));
    }

    /// <summary>
    /// Gli alfabeti non latini restano intatti: senza la certezza che il portale li rifiuti,
    /// cancellarli renderebbe illeggibile il nome di un ospite straniero.
    /// </summary>
    [Fact]
    public void Normalizza_AlfabetiNonLatini_Intatti()
    {
        Assert.Equal("ИВАНОВ", TestoTracciato.Normalizza(" ИВАНОВ "));
    }
}
