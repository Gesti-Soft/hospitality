using GestiSoft.Application.Trattamenti;
using GestiSoft.Domain.Entities;
using GestiSoft.Domain.Enums;

namespace GestiSoft.Application.SuperAdmin;

/// <param name="Cliente">Il Cliente demo da creare, null se la demo va a un Cliente esistente.</param>
public record DatiStrutturaDemo(
    Cliente? Cliente,
    Struttura Struttura,
    ImpostazioniStruttura Impostazioni,
    DatiAziendali DatiAziendali,
    IReadOnlyList<TrattamentoStruttura> Trattamenti,
    IReadOnlyList<SettingTipologia> Tipologie,
    IReadOnlyList<SettingRoom> Camere,
    IReadOnlyList<SettingAgenzia> Canali,
    IReadOnlyList<Prenotazione> Prenotazioni,
    IReadOnlyList<Ospite> Ospiti,
    IReadOnlyList<OspiteRiga> OspitiRighe,
    IReadOnlyList<PagamentoPrenotazione> Pagamenti,
    IReadOnlyList<Cauzione> Cauzioni,
    IReadOnlyList<Entrata> Entrate,
    IReadOnlyList<Spesa> Spese);

/// <summary>
/// Genera "Hotel Belvedere", la struttura dimostrativa: stessa logica dello script usato per le
/// schermate (screenshot-gestisoft/_script/genera.py) ma con 20 camere invece di 40 e le date
/// riferite a oggi, così la demo creata in produzione ha sempre arrivi, partenze e ospiti in casa.
///
/// Anagrafiche tutte inventate, nessun numero di documento. Nessuna integrazione: OTA, Alloggiati
/// Web, Osservatorio e PayTourist restano non concessi e spenti, quindi la demo non entra mai negli
/// invii automatici né nei push verso l'OTA.
/// </summary>
public static class StrutturaDemoGenerator
{
    public const int NumeroCamere = 20;

    private const decimal TassaSoggiorno = 2.50m;
    private const int TassaMaxNotti = 7;
    private const int EtaEsenzioneMinori = 12;
    private const decimal SupplementoAnimali = 15.00m;

    // Nome, prezzo base, pulizia, cauzione, ospiti inclusi, supplemento per persona, quante camere.
    private static readonly (string Nome, decimal Prezzo, decimal Pulizia, decimal Cauzione, int Inclusi, decimal Supplemento, int Quante)[] TipologieDemo =
    [
        ("Camera Singola", 78.00m, 15.00m, 0.00m, 1, 0.00m, 2),
        ("Camera Doppia Classic", 118.00m, 20.00m, 50.00m, 2, 25.00m, 6),
        ("Camera Doppia Superior", 152.00m, 20.00m, 50.00m, 2, 28.00m, 5),
        ("Camera Tripla", 178.00m, 25.00m, 50.00m, 3, 28.00m, 2),
        ("Family Room", 205.00m, 30.00m, 80.00m, 4, 30.00m, 2),
        ("Junior Suite", 238.00m, 30.00m, 80.00m, 2, 35.00m, 2),
        ("Suite Vista Mare", 310.00m, 40.00m, 100.00m, 2, 40.00m, 1),
    ];

    private static readonly (string Nome, string Colore, int Peso)[] CanaliDemo =
    [
        ("Diretta", "#2E7D32", 22),
        ("Booking.com", "#1565C0", 36),
        ("Airbnb", "#C2185B", 15),
        ("Expedia", "#F9A825", 10),
        ("Agenzia", "#6A1B9A", 9),
        ("Telefono", "#00838F", 8),
    ];

    // Nome e cognome dallo stesso paese: "Piotr Smith" in una demo si nota e toglie credibilità.
    private static readonly Dictionary<string, (string[] Maschili, string[] Femminili, string[] Cognomi)> Anagrafiche = new()
    {
        ["ITALIA"] = (
            ["Marco", "Luca", "Giuseppe", "Andrea", "Francesco", "Alessandro", "Matteo", "Davide", "Stefano", "Antonio", "Salvatore", "Riccardo", "Federico", "Lorenzo"],
            ["Giulia", "Chiara", "Francesca", "Sara", "Martina", "Elena", "Alessia", "Valentina", "Anna", "Laura", "Silvia", "Roberta", "Federica", "Marta"],
            ["Rossi", "Russo", "Ferrari", "Esposito", "Bianchi", "Romano", "Colombo", "Ricci", "Marino", "Greco", "Bruno", "Gallo", "Conti", "De Luca", "Costa", "Giordano", "Mancini", "Rizzo", "Lombardi", "Moretti", "Barbieri", "Fontana", "Caruso", "Ferrara"]),
        ["GERMANIA"] = (
            ["Thomas", "Michael", "Andreas", "Stefan", "Lukas", "Jonas", "Matthias", "Sebastian"],
            ["Julia", "Lena", "Hanna", "Sabine", "Katrin", "Anja", "Nicole", "Petra"],
            ["Muller", "Schmidt", "Weber", "Fischer", "Wagner", "Becker", "Hoffmann", "Schneider"]),
        ["FRANCIA"] = (
            ["Pierre", "Nicolas", "Julien", "Laurent", "Olivier", "Antoine", "Mathieu", "Guillaume"],
            ["Marie", "Claire", "Camille", "Sophie", "Elise", "Juliette", "Nathalie", "Celine"],
            ["Dubois", "Martin", "Bernard", "Petit", "Durand", "Moreau", "Lefevre", "Girard"]),
        ["REGNO UNITO"] = (
            ["James", "Oliver", "Harry", "Thomas", "George", "Daniel", "William", "Jack"],
            ["Lucy", "Emily", "Charlotte", "Sophie", "Rachel", "Hannah", "Olivia", "Grace"],
            ["Smith", "Jones", "Taylor", "Brown", "Wilson", "Davies", "Evans", "Walker"]),
        ["STATI UNITI D'AMERICA"] = (
            ["Michael", "John", "David", "Robert", "Brian", "Kevin", "Steven", "Eric"],
            ["Jennifer", "Sarah", "Ashley", "Emily", "Rachel", "Laura", "Michelle", "Amanda"],
            ["Miller", "Davis", "Johnson", "Anderson", "Thompson", "Clark", "Harris", "Lewis"]),
        ["PAESI BASSI"] = (
            ["Jan", "Pieter", "Bram", "Daan", "Sven", "Ruben", "Tim", "Lars"],
            ["Anke", "Femke", "Sanne", "Lotte", "Eva", "Marieke", "Iris", "Noor"],
            ["De Vries", "Van Dijk", "Jansen", "Bakker", "Visser", "Smit", "Meijer", "De Boer"]),
        ["SVIZZERA"] = (
            ["Andreas", "Marco", "Reto", "Matthias", "Lukas", "Daniel", "Stefan", "Christian"],
            ["Nicole", "Sandra", "Claudia", "Anja", "Simone", "Barbara", "Andrea", "Karin"],
            ["Keller", "Meier", "Steiner", "Brunner", "Widmer", "Huber", "Frei", "Baumann"]),
        ["SPAGNA"] = (
            ["Javier", "Carlos", "Alejandro", "Sergio", "Pablo", "Miguel", "Alvaro", "Daniel"],
            ["Lucia", "Carmen", "Marta", "Elena", "Paula", "Sofia", "Ana", "Laura"],
            ["Garcia", "Lopez", "Martinez", "Sanchez", "Fernandez", "Gonzalez", "Ruiz", "Romero"]),
        ["BELGIO"] = (
            ["Bart", "Kevin", "Nicolas", "Thomas", "Wouter", "Maxime", "Jeroen", "Arnaud"],
            ["Elise", "Charlotte", "Marie", "Femke", "Sofie", "Laura", "Julie", "Anouk"],
            ["Peeters", "Janssens", "Maes", "Willems", "Claes", "Dupont", "Lambert", "Mertens"]),
        ["POLONIA"] = (
            ["Piotr", "Tomasz", "Marcin", "Jakub", "Michal", "Andrzej", "Krzysztof", "Pawel"],
            ["Katarzyna", "Agnieszka", "Magdalena", "Anna", "Joanna", "Ewa", "Monika", "Zofia"],
            ["Nowak", "Kowalski", "Wisniewski", "Wojcik", "Kaminski", "Lewandowski", "Zielinski", "Dabrowski"]),
    };

    private static readonly (string Nome, string Iso, int Peso)[] Nazioni =
    [
        ("ITALIA", "IT", 42), ("GERMANIA", "DE", 12), ("FRANCIA", "FR", 10), ("REGNO UNITO", "GB", 9),
        ("STATI UNITI D'AMERICA", "US", 7), ("PAESI BASSI", "NL", 6), ("SVIZZERA", "CH", 5),
        ("SPAGNA", "ES", 4), ("BELGIO", "BE", 3), ("POLONIA", "PL", 2),
    ];

    private static readonly (string Comune, string Provincia)[] ComuniItaliani =
    [
        ("PALERMO", "PA"), ("MILANO", "MI"), ("ROMA", "RM"), ("TORINO", "TO"), ("NAPOLI", "NA"), ("BOLOGNA", "BO"),
        ("FIRENZE", "FI"), ("CATANIA", "CT"), ("BERGAMO", "BG"), ("VERONA", "VR"), ("PADOVA", "PD"), ("GENOVA", "GE"),
        ("BARI", "BA"), ("TRAPANI", "TP"), ("MESSINA", "ME"), ("BRESCIA", "BS"),
    ];

    private static readonly string[] DocumentiItaliani = ["CARTA DI IDENTITA'", "CARTA IDENTITA' ELETTRONICA", "PASSAPORTO ORDINARIO", "PATENTE DI GUIDA"];

    // Sicilia: bassa stagione d'inverno, picco a luglio/agosto, coda lunga a settembre.
    private static readonly double[] Occupazione = [0, 0.26, 0.29, 0.37, 0.54, 0.66, 0.79, 0.92, 0.95, 0.81, 0.56, 0.31, 0.38];

    // Moltiplicatore del prezzo per mese: l'alta stagione costa di più.
    private static readonly decimal[] Stagionalita = [0, 0.72m, 0.74m, 0.82m, 0.95m, 1.05m, 1.25m, 1.55m, 1.80m, 1.20m, 0.92m, 0.72m, 0.85m];

    // Importi mensili di un albergo da 20 camere (metà di quelli dello script da 40): costi intorno ai
    // due terzi del fatturato, come in un albergo vero.
    private static readonly (string Nome, string Descrizione, double Min, double Max)[] VociEntrata =
    [
        ("Ristorante", "Coperti ristorante", 3000, 8000),
        ("Bar e minibar", "Consumazioni bar", 1000, 3250),
        ("Transfer aeroporto", "Servizio navetta ospiti", 250, 1100),
        ("Escursioni", "Escursioni in barca e visite guidate", 450, 1900),
        ("Spiaggia", "Noleggio ombrelloni e lettini", 550, 2250),
        ("Lavanderia ospiti", "Servizio lavanderia", 125, 550),
        ("Parcheggio", "Posti auto custoditi", 400, 1400),
    ];

    private static readonly (string Nome, string Descrizione, double Min, double Max)[] VociSpesa =
    [
        ("Personale", "Stipendi e contributi", 16000, 26000),
        ("Food & beverage", "Acquisto alimenti e bevande", 4000, 10000),
        ("Commissioni portali", "Commissioni canali di vendita", 4500, 14000),
        ("Utenze", "Energia elettrica, acqua e gas", 2000, 5500),
        ("Lavanderia", "Biancheria e lavanderia esterna", 1000, 3000),
        ("Manutenzione", "Interventi ordinari e riparazioni", 750, 3500),
        ("Forniture", "Prodotti pulizia e cortesia", 600, 2250),
        ("Marketing", "Campagne e fotografie", 500, 2500),
    ];

    private static readonly string[] MetodiSpesa = ["Bonifico bancario", "Carta", "Contanti"];

    private static readonly ModalitaPagamento[] MetodiIncasso = [ModalitaPagamento.CartaDiPagamento, ModalitaPagamento.CartaDiPagamento, ModalitaPagamento.Bonifico, ModalitaPagamento.Contanti];

    /// <param name="oggi">Data civile italiana di oggi (vedi PulizieSoggiornoService.Oggi).</param>
    /// <param name="clienteId">Cliente a cui assegnare la demo; null = se ne crea uno demo nuovo.</param>
    /// <param name="nomeClienteNuovo">Nome del Cliente demo nuovo (solo senza <paramref name="clienteId"/>).</param>
    public static DatiStrutturaDemo Genera(DateTime oggi, int seme, Guid? clienteId = null, string? nomeClienteNuovo = null)
    {
        oggi = oggi.Date;
        var random = new Random(seme);

        var cliente = clienteId is null
            ? new Cliente
            {
                RagioneSociale = string.IsNullOrWhiteSpace(nomeClienteNuovo) ? "Hotel Belvedere S.r.l. (demo)" : nomeClienteNuovo.Trim(),
                Note = "Dati dimostrativi, generati dal pulsante \"Crea struttura demo\". Anagrafiche inventate.",
                Demo = true,
            }
            : null;

        var struttura = new Struttura { ClienteId = clienteId ?? cliente!.Id, Nome = "Hotel Belvedere", Demo = true };

        var impostazioni = new ImpostazioniStruttura
        {
            StrutturaId = struttura.Id,
            TassaSoggiornoPrezzo = TassaSoggiorno,
            TassaSoggiornoMaxGiorni = TassaMaxNotti,
            TassaSoggiornoEtaEsenzioneMinori = EtaEsenzioneMinori,
            ComuneAttivita = "CEFALU'",
        };

        var datiAziendali = new DatiAziendali
        {
            StrutturaId = struttura.Id,
            Iso2 = "IT",
            Denominazione = "Hotel Belvedere S.r.l.",
            RegimeFiscale = RegimeFiscale.RF01_Ordinario,
            AliquotaIvaDefault = AliquotaIva.Iva10,
            Indirizzo = "Lungomare Giuseppe Giardina",
            NCivico = "44",
            Cap = "90015",
            Comune = "CEFALU'",
            Provincia = "PA",
            Nazione = "ITALIA",
        };

        var trattamenti = TrattamentiService.BaseDellaStruttura(struttura.Id).ToList();

        var tipologie = new List<SettingTipologia>();
        var assegnazione = new List<(SettingTipologia Tipologia, int Inclusi, decimal Prezzo, decimal Pulizia, decimal Cauzione, decimal Supplemento)>();
        foreach (var t in TipologieDemo)
        {
            var tipologia = new SettingTipologia
            {
                StrutturaId = struttura.Id,
                TipologiaCamera = t.Nome,
                SpesePulizia = t.Pulizia,
                Animali = SupplementoAnimali,
                Cauzione = t.Cauzione,
                PrezzoDefault = t.Prezzo,
                NumeroImplementoPersona = t.Inclusi,
                Implemento = t.Supplemento,
            };
            tipologie.Add(tipologia);
            for (var i = 0; i < t.Quante; i++)
            {
                assegnazione.Add((tipologia, t.Inclusi, t.Prezzo, t.Pulizia, t.Cauzione, t.Supplemento));
            }
        }

        // 20 camere su 2 piani: 101..110, 201..210.
        var numeri = new[] { 1, 2 }.SelectMany(piano => Enumerable.Range(1, 10).Select(stanza => $"{piano}{stanza:00}")).ToList();
        if (numeri.Count != NumeroCamere || assegnazione.Count != NumeroCamere)
        {
            throw new InvalidOperationException("Numero di camere della demo incoerente.");
        }

        var camere = new List<(SettingRoom Camera, int Inclusi, decimal Prezzo, decimal Pulizia, decimal Cauzione, decimal Supplemento, int Capacita)>();
        for (var i = 0; i < NumeroCamere; i++)
        {
            var a = assegnazione[i];
            // Un letto aggiunto, non due: una doppia che dichiara di dormirne quattro non è una doppia.
            var capacita = a.Inclusi + (a.Supplemento > 0 ? 1 : 0);
            var camera = new SettingRoom
            {
                StrutturaId = struttura.Id,
                TipologiaId = a.Tipologia.Id,
                Nome = numeri[i],
                StateRoom = StatoCamera.Pronta,
                CapacitaOspiti = capacita,
                SoggiornoMinimo = 1,
            };
            camere.Add((camera, a.Inclusi, a.Prezzo, a.Pulizia, a.Cauzione, a.Supplemento, capacita));
        }

        var canali = CanaliDemo.Select(c => new SettingAgenzia { StrutturaId = struttura.Id, Descrizione = c.Nome, Colore = c.Colore }).ToList();

        var prenotazioni = new List<Prenotazione>();
        var ospiti = new List<Ospite>();
        var righe = new List<OspiteRiga>();
        var pagamenti = new List<PagamentoPrenotazione>();
        var cauzioni = new List<Cauzione>();

        var inizio = new DateTime(oggi.Year - 1, 1, 1);
        var fine = oggi.AddMonths(6);

        foreach (var c in camere)
        {
            var cursore = inizio;
            while (cursore < fine)
            {
                var mese = cursore.Month;
                var notti = DurataSoggiorno(random, mese);
                var arrivo = cursore;
                var partenza = arrivo.AddDays(notti);

                // Qualche arrivo e qualche partenza proprio oggi, così la giornata non è vuota.
                if (Math.Abs((arrivo - oggi).Days) == 1 && random.NextDouble() < 0.55)
                {
                    arrivo = oggi;
                    partenza = arrivo.AddDays(notti);
                }
                else if (Math.Abs((partenza - oggi).Days) == 1 && random.NextDouble() < 0.5 && notti > 1)
                {
                    partenza = oggi;
                    notti = (partenza - arrivo).Days;
                }

                if (notti < 1)
                {
                    cursore = cursore.AddDays(1);
                    continue;
                }

                var ospitiTotali = Math.Min(c.Capacita, Math.Max(1, c.Inclusi + Scegli(random, [0, 0, 0, 1, 1, 2])));
                var minori = ospitiTotali >= 3 ? Scegli(random, [0, 0, 0, 1]) : 0;
                var adulti = ospitiTotali - minori;

                var prezzoNotte = Math.Round(c.Prezzo * Stagionalita[arrivo.Month], 2);
                var extraPersone = Math.Max(0, ospitiTotali - c.Inclusi);
                var importo = Math.Round(prezzoNotte * notti + extraPersone * c.Supplemento * notti, 2);
                var totale = importo + c.Pulizia;
                var tassa = TassaSoggiorno * adulti * Math.Min(notti, TassaMaxNotti);

                var canale = ScegliPesato(random, CanaliDemo, x => x.Peso).Nome;
                var nazione = ScegliPesato(random, Nazioni, x => x.Peso);

                // Data in cui è stata presa la prenotazione: mai nel futuro.
                var prenotataIl = arrivo.AddDays(-random.Next(3, 91));
                if (prenotataIl > oggi)
                {
                    prenotataIl = oggi.AddDays(-random.Next(0, 31));
                }

                // Lo stato racconta a che punto è la prenotazione stamattina: chi parte oggi per lo più
                // è ancora dentro e chi arriva oggi per lo più è ancora da registrare, così la giornata
                // ha qualcosa da fare.
                StatoPrenotazione stato;
                decimal pagato;
                DateTime? checkInFatto = null;
                var annullata = random.NextDouble() < 0.025;
                if (annullata)
                {
                    stato = StatoPrenotazione.Annullata;
                    pagato = 0;
                }
                else if (partenza < oggi)
                {
                    stato = StatoPrenotazione.Completata;
                    pagato = totale;
                    checkInFatto = arrivo.AddHours(13).AddMinutes(30);
                }
                else if (partenza == oggi)
                {
                    checkInFatto = arrivo.AddHours(14).AddMinutes(10);
                    if (random.NextDouble() < 0.55)
                    {
                        stato = StatoPrenotazione.InCorso;
                        pagato = Math.Round(totale * Scegli(random, [0.5m, 1.0m]), 2);
                        c.Camera.StateRoom = StatoCamera.Occupata;
                    }
                    else
                    {
                        stato = StatoPrenotazione.Completata;
                        pagato = totale;
                        if (c.Camera.StateRoom != StatoCamera.Occupata)
                        {
                            c.Camera.StateRoom = StatoCamera.DaPulire;
                        }
                    }
                }
                else if (arrivo == oggi)
                {
                    if (random.NextDouble() < 0.7)
                    {
                        stato = StatoPrenotazione.Incompleta;
                        pagato = random.NextDouble() < 0.6 ? Math.Round(totale * 0.30m, 2) : 0;
                    }
                    else
                    {
                        stato = StatoPrenotazione.InCorso;
                        pagato = Math.Round(totale * Scegli(random, [0.3m, 0.5m, 1.0m]), 2);
                        checkInFatto = arrivo.AddHours(12).AddMinutes(40);
                        c.Camera.StateRoom = StatoCamera.Occupata;
                    }
                }
                else if (arrivo < oggi && oggi < partenza)
                {
                    stato = StatoPrenotazione.InCorso;
                    pagato = Math.Round(totale * Scegli(random, [0.3m, 0.5m, 1.0m]), 2);
                    checkInFatto = arrivo.AddHours(14).AddMinutes(10);
                    c.Camera.StateRoom = StatoCamera.Occupata;
                }
                else
                {
                    stato = StatoPrenotazione.Incompleta;
                    pagato = random.NextDouble() < 0.55 ? Math.Round(totale * 0.30m, 2) : 0;
                }

                var prenotazione = new Prenotazione
                {
                    StrutturaId = struttura.Id,
                    CameraId = c.Camera.Id,
                    TipologiaId = c.Camera.TipologiaId,
                    Agenzia = canale,
                    ImportoPrenotazione = stato == StatoPrenotazione.Annullata ? 0 : importo,
                    ImportoTotale = stato == StatoPrenotazione.Annullata ? 0 : totale,
                    ImportoPagato = pagato,
                    CheckIn = Utc(arrivo),
                    CheckOut = Utc(partenza),
                    CheckInEffettuatoAtUtc = checkInFatto is { } fatto ? Utc(fatto) : null,
                    NumeroOspiti = ospitiTotali,
                    Anno = arrivo.Year,
                    TotalTax = stato == StatoPrenotazione.Annullata ? 0 : tassa,
                    StatoPrenotazione = stato,
                    CauzioneAttiva = c.Cauzione > 0,
                    SpesePuliziaAttiva = true,
                    TassaSoggiornoAttiva = true,
                    AnimaliAttiva = false,
                    CreatedAtUtc = Utc(prenotataIl.AddHours(9)),
                };
                prenotazioni.Add(prenotazione);

                // Un solo Ospite per prenotazione (il capofamiglia, o chi viaggia da solo): gli
                // accompagnatori sono righe di schedina collegate a lui.
                var capo = Persona(random, nazione.Nome, nazione.Iso, arrivo, etaMinore: null);
                var ospite = new Ospite
                {
                    StrutturaId = struttura.Id,
                    PrenotazioneId = prenotazione.Id,
                    TipoOspite = ospitiTotali == 1 ? "OSPITE SINGOLO" : "CAPO FAMIGLIA",
                    Permanenza = notti,
                    DataNascita = Utc(capo.Nascita),
                    Sesso = capo.Sesso,
                    Cognome = capo.Cognome.ToUpperInvariant(),
                    Nome = capo.Nome.ToUpperInvariant(),
                    Cittadinanza = nazione.Nome,
                    LuogoNascita = capo.Luogo,
                    StatoNascita = nazione.Nome,
                    LuogoResidenza = capo.Luogo,
                    Documento = nazione.Nome == "ITALIA" ? Scegli(random, DocumentiItaliani) : "PASSAPORTO ORDINARIO",
                    CreatedAtUtc = prenotazione.CreatedAtUtc,
                };
                ospiti.Add(ospite);

                for (var indice = 1; indice < ospitiTotali; indice++)
                {
                    var minore = indice >= adulti;
                    int? eta = minore ? random.Next(3, EtaEsenzioneMinori) : null;
                    var dati = Persona(random, nazione.Nome, nazione.Iso, arrivo, eta);
                    if (eta is { } e)
                    {
                        prenotazione.EtaBambini.Add(e);
                    }

                    righe.Add(new OspiteRiga
                    {
                        StrutturaId = struttura.Id,
                        OspiteId = ospite.Id,
                        CameraId = c.Camera.Id,
                        Permanenza = notti,
                        DataNascita = Utc(dati.Nascita),
                        Sesso = dati.Sesso,
                        // I figli portano il cognome del capofamiglia.
                        Cognome = (minore ? capo.Cognome : dati.Cognome).ToUpperInvariant(),
                        Nome = dati.Nome.ToUpperInvariant(),
                        Cittadinanza = nazione.Nome,
                        LuogoNascita = dati.Luogo,
                        StatoNascita = nazione.Nome,
                        LuogoResidenza = dati.Luogo,
                        PostoLetto = true,
                        EsenteDaTassa = minore,
                        CreatedAtUtc = prenotazione.CreatedAtUtc,
                    });
                }

                // ImportoPagato è la somma del registro dei pagamenti: ogni euro incassato ha la sua riga.
                if (pagato > 0)
                {
                    pagamenti.Add(new PagamentoPrenotazione
                    {
                        StrutturaId = struttura.Id,
                        PrenotazioneId = prenotazione.Id,
                        Data = DateOnly.FromDateTime(stato == StatoPrenotazione.Completata ? partenza : (checkInFatto ?? prenotataIl).Date),
                        Importo = pagato,
                        Tipo = pagato == totale ? TipoPagamento.Saldo : TipoPagamento.Acconto,
                        Metodo = Scegli(random, MetodiIncasso),
                        RegistratoDa = "Demo",
                    });
                }

                // Cauzione registrata al check-out, solo su una parte delle prenotazioni chiuse.
                if (stato == StatoPrenotazione.Completata && c.Cauzione > 0 && random.NextDouble() < 0.35)
                {
                    cauzioni.Add(new Cauzione
                    {
                        StrutturaId = struttura.Id,
                        PrenotazioneId = prenotazione.Id,
                        ImportoCauzione = c.Cauzione,
                        DataInserimento = Utc(partenza),
                    });
                }

                var occupazione = Occupazione[mese];
                var pausa = Math.Max(1, (int)Math.Round(notti * (1 - occupazione) / occupazione));
                pausa = Math.Max(1, (int)Gauss(random, pausa, 1));
                cursore = partenza.AddDays(pausa);
            }
        }

        // Il numero prenotazione delle dirette è progressivo per anno e in ordine di arrivo (lo stesso
        // progressivo che l'app continua, vedi PrenotazioniService.ProssimoNumeroDiretta); gli altri
        // canali hanno il codice del portale.
        var progressivi = new Dictionary<int, int>();
        foreach (var p in prenotazioni.OrderBy(p => p.CheckIn).ThenBy(p => p.CameraId))
        {
            p.NumeroPrenotazione = p.Agenzia switch
            {
                "Diretta" => (progressivi[p.Anno] = progressivi.GetValueOrDefault(p.Anno) + 1).ToString(),
                "Booking.com" => random.NextInt64(4_000_000_000, 5_000_000_000).ToString(),
                "Airbnb" => "HM" + new string(Enumerable.Range(0, 8).Select(_ => (char)('A' + random.Next(26))).ToArray()),
                "Expedia" => random.Next(700_000_000, 800_000_000).ToString(),
                "Agenzia" => $"AG-{p.Anno}-{random.Next(1000, 10000)}",
                _ => $"TEL-{random.Next(10000, 100000)}",
            };
        }

        var entrate = new List<Entrata>();
        var spese = new List<Spesa>();
        for (var mese = new DateTime(oggi.Year - 1, 1, 1); mese <= oggi; mese = mese.AddMonths(1))
        {
            var peso = Occupazione[mese.Month];
            var ultimoGiorno = mese.Year == oggi.Year && mese.Month == oggi.Month ? Math.Min(28, oggi.Day) : 28;
            foreach (var v in VociEntrata)
            {
                var data = mese.AddDays(random.Next(0, ultimoGiorno));
                entrate.Add(new Entrata
                {
                    StrutturaId = struttura.Id,
                    TipoEntrata = "Generali",
                    Nome = v.Nome,
                    Descrizione = v.Descrizione,
                    ImportoEntrata = Math.Round((decimal)(Uniforme(random, v.Min, v.Max) * (0.6 + peso)), 2),
                    Data = Utc(data),
                    Anno = data.Year,
                });
            }

            foreach (var v in VociSpesa)
            {
                var data = mese.AddDays(random.Next(0, ultimoGiorno));
                spese.Add(new Spesa
                {
                    StrutturaId = struttura.Id,
                    TipoSpesa = "Generali",
                    Nome = v.Nome,
                    Descrizione = v.Descrizione,
                    ImportoSpesa = Math.Round((decimal)(Uniforme(random, v.Min, v.Max) * (0.7 + peso * 0.6)), 2),
                    MetodoPagamento = Scegli(random, MetodiSpesa),
                    DataSpesa = Utc(data),
                    Anno = data.Year,
                });
            }
        }

        return new DatiStrutturaDemo(
            cliente, struttura, impostazioni, datiAziendali, trattamenti, tipologie,
            camere.Select(c => c.Camera).ToList(), canali, prenotazioni, ospiti, righe, pagamenti, cauzioni, entrate, spese);
    }

    private static int DurataSoggiorno(Random random, int mese) => mese switch
    {
        7 or 8 => Scegli(random, [3, 4, 5, 5, 6, 7, 7, 8, 10, 14]),
        5 or 6 or 9 => Scegli(random, [2, 3, 3, 4, 5, 5, 6, 7]),
        _ => Scegli(random, [1, 2, 2, 3, 3, 4, 5]),
    };

    private static (string Nome, string Cognome, Sesso Sesso, DateTime Nascita, string Luogo) Persona(
        Random random, string nazione, string iso, DateTime arrivo, int? etaMinore)
    {
        var sesso = random.Next(2) == 0 ? Sesso.Maschio : Sesso.Femmina;
        var (maschili, femminili, cognomi) = Anagrafiche[nazione];
        var nome = Scegli(random, sesso == Sesso.Maschio ? maschili : femminili);
        var cognome = Scegli(random, cognomi);

        // L'età del minore è quella all'arrivo, la stessa che finisce tra le età dei bambini.
        var nascita = etaMinore is { } eta
            ? arrivo.AddYears(-eta).AddDays(-random.Next(1, 360))
            : new DateTime(random.Next(1955, arrivo.Year - 19), random.Next(1, 13), random.Next(1, 29));

        string luogo;
        if (nazione == "ITALIA")
        {
            var (comune, provincia) = Scegli(random, ComuniItaliani);
            luogo = $"{comune} ({provincia})";
        }
        else
        {
            luogo = $"{nazione} ({iso})";
        }

        return (nome, cognome, sesso, nascita, luogo);
    }

    private static T Scegli<T>(Random random, IReadOnlyList<T> valori) => valori[random.Next(valori.Count)];

    private static T ScegliPesato<T>(Random random, IReadOnlyList<T> valori, Func<T, int> peso)
    {
        var estratto = random.Next(valori.Sum(peso));
        foreach (var valore in valori)
        {
            estratto -= peso(valore);
            if (estratto < 0)
            {
                return valore;
            }
        }

        return valori[^1];
    }

    private static double Uniforme(Random random, double minimo, double massimo) => minimo + random.NextDouble() * (massimo - minimo);

    private static double Gauss(Random random, double media, double deviazione)
    {
        var u1 = 1.0 - random.NextDouble();
        var u2 = random.NextDouble();
        return media + deviazione * Math.Sqrt(-2.0 * Math.Log(u1)) * Math.Cos(2.0 * Math.PI * u2);
    }

    /// <summary>Le date del soggiorno sono date civili salvate a mezzanotte UTC, come fa il resto dell'app.</summary>
    private static DateTime Utc(DateTime data) => DateTime.SpecifyKind(data, DateTimeKind.Utc);
}
