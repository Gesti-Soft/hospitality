using GestiSoft.Application.Auth;
using GestiSoft.Application.Camere;

namespace GestiSoft.Application.Statistiche;

public record StatisticheKpiResult(int NumeroPrenotazioni, decimal RicavoStimato, decimal RicavoEffettivo, double? PermanenzaMediaNotti, double TassoOccupazionePercentuale);

public record VoceConteggioResult(string Etichetta, int Conteggio);

public record VocePercentualeResult(string Etichetta, int Conteggio, double Percentuale);

public record ValoreMensileResult(int Mese, decimal Valore);

public record VoceImportoResult(string Etichetta, decimal Importo);

public record RigaMatriceMeseResult(int Mese, IReadOnlyList<int> ConteggiPerTipologia);

public record MatricePrenotazioniTipologiaResult(IReadOnlyList<string> Tipologie, IReadOnlyList<RigaMatriceMeseResult> Righe);

public record StatisticheTassaSoggiornoResult(decimal TotaleAnno, IReadOnlyList<ValoreMensileResult> AndamentoMensile);

public record StatisticheStrutturaResult(
    int Anno,
    StatisticheKpiResult Kpi,
    IReadOnlyList<VoceConteggioResult> PrenotazioniPerAgenzia,
    IReadOnlyList<VocePercentualeResult> PrenotazioniPerNazionalita,
    IReadOnlyList<ValoreMensileResult> AndamentoRicavoMensile,
    IReadOnlyList<VoceImportoResult> RicavoPerTipologiaCamera,
    MatricePrenotazioniTipologiaResult PrenotazioniPerTipologiaMese,
    StatisticheTassaSoggiornoResult TassaSoggiorno);

/// <summary>
/// Pagina "Statistiche" per Struttura — porta le formule di DashBoardViewModel.cs del gestionale
/// legacy (WPF, KPI + 6 grafici: prenotazioni/ricavo per anno, permanenza media, tasso occupazione,
/// prenotazioni per agenzia/nazionalità, andamento ricavo mensile, ricavo e prenotazioni per
/// tipologia camera) più una sezione tassa di soggiorno assente nel legacy. Sola lettura: nessuna
/// scrittura, stesso permesso di Finanze (FinanceRead) perché la pagina espone soprattutto dati
/// economici.
/// </summary>
public class StatisticheService(
    IStatisticheRepository statistiche,
    ICameraRepository camere,
    ITipologiaCameraRepository tipologie,
    PermessoStrutturaGuard permessoGuard)
{
    private const int MassimoVociTopN = 7;

    /// <summary>Anni con almeno una prenotazione per questa Struttura — su richiesta esplicita, il selettore anno non deve proporre anni sicuramente vuoti.</summary>
    public async Task<IReadOnlyList<int>> GetAnniDisponibiliAsync(ICurrentUser currentUser, Guid strutturaId, CancellationToken cancellationToken)
    {
        await permessoGuard.EnsureAsync(currentUser, strutturaId, p => p.FinanceRead, cancellationToken);
        return await statistiche.ListaAnniConDatiAsync(strutturaId, cancellationToken);
    }

    public async Task<StatisticheStrutturaResult> GetStatisticheAsync(ICurrentUser currentUser, Guid strutturaId, int anno, CancellationToken cancellationToken)
    {
        await permessoGuard.EnsureAsync(currentUser, strutturaId, p => p.FinanceRead, cancellationToken);

        var numeroPrenotazioni = await statistiche.ContaPrenotazioniAnnoAsync(strutturaId, anno, cancellationToken);
        var (ricavoStimato, ricavoEffettivo) = await statistiche.SommaImportiAnnoAsync(strutturaId, anno, cancellationToken);
        var permanenzaMedia = await statistiche.PermanenzaMediaAsync(strutturaId, anno, cancellationToken);
        var sommaPermanenza = await statistiche.SommaPermanenzaAnnoAsync(strutturaId, anno, cancellationToken);
        var numeroCamere = (await camere.ListByStrutturaAsync(strutturaId, cancellationToken)).Count;
        var tassoOccupazione = CalcoloOccupazione.TassoOccupazionePercentuale(sommaPermanenza, numeroCamere, anno);

        var perAgenzia = await statistiche.ContaPerAgenziaAsync(strutturaId, anno, cancellationToken);
        var perNazionalita = await statistiche.ContaPerNazionalitaAsync(strutturaId, anno, cancellationToken);
        var ricavoMensile = await statistiche.RicavoMensileAsync(strutturaId, anno, cancellationToken);
        var ricavoPerTipologia = await statistiche.RicavoPerTipologiaAsync(strutturaId, anno, cancellationToken);
        var matriceGrezza = await statistiche.PrenotazioniPerTipologiaMeseAsync(strutturaId, anno, cancellationToken);
        var tassaTotale = await statistiche.SommaTassaSoggiornoAnnoAsync(strutturaId, anno, cancellationToken);
        var tassaMensile = await statistiche.TassaSoggiornoMensileAsync(strutturaId, anno, cancellationToken);

        var tipologieStruttura = (await tipologie.ListByStrutturaAsync(strutturaId, cancellationToken))
            .Select(t => t.TipologiaCamera)
            .OrderBy(nome => nome, StringComparer.OrdinalIgnoreCase)
            .ToList();

        var kpi = new StatisticheKpiResult(numeroPrenotazioni, ricavoStimato, ricavoEffettivo, permanenzaMedia, tassoOccupazione);

        return new StatisticheStrutturaResult(
            anno,
            kpi,
            Top7PiuAltroConteggio(perAgenzia.Select(v => (v.Etichetta ?? "Sconosciuta", v.Conteggio))),
            ConPercentuali(perNazionalita, numeroPrenotazioni),
            CompletaAiDodiciMesi(ricavoMensile),
            Top7PiuAltroImporto(ricavoPerTipologia),
            CostruisciMatrice(tipologieStruttura, matriceGrezza),
            new StatisticheTassaSoggiornoResult(tassaTotale, CompletaAiDodiciMesi(tassaMensile)));
    }

    internal static IReadOnlyList<VoceConteggioResult> Top7PiuAltroConteggio(IEnumerable<(string Etichetta, int Conteggio)> voci)
    {
        var ordinate = voci.OrderByDescending(v => v.Conteggio).ToList();
        var top = ordinate.Take(MassimoVociTopN).Select(v => new VoceConteggioResult(v.Etichetta, v.Conteggio)).ToList();

        var resto = ordinate.Skip(MassimoVociTopN).Sum(v => v.Conteggio);
        if (resto > 0)
        {
            top.Add(new VoceConteggioResult("Altro", resto));
        }

        return top;
    }

    internal static IReadOnlyList<VoceImportoResult> Top7PiuAltroImporto(IEnumerable<(string Tipologia, decimal Totale)> voci)
    {
        var ordinate = voci.OrderByDescending(v => v.Totale).ToList();
        var top = ordinate.Take(MassimoVociTopN).Select(v => new VoceImportoResult(v.Tipologia, v.Totale)).ToList();

        var resto = ordinate.Skip(MassimoVociTopN).Sum(v => v.Totale);
        if (resto > 0)
        {
            top.Add(new VoceImportoResult("Altro", resto));
        }

        return top;
    }

    internal static IReadOnlyList<VocePercentualeResult> ConPercentuali(IReadOnlyList<(string? Etichetta, int Conteggio)> voci, int totale)
    {
        var ordinate = voci.OrderByDescending(v => v.Conteggio).ToList();
        var top = ordinate.Take(MassimoVociTopN)
            .Select(v => new VocePercentualeResult(v.Etichetta ?? "Sconosciuto", v.Conteggio, Percentuale(v.Conteggio, totale)))
            .ToList();

        var resto = ordinate.Skip(MassimoVociTopN).Sum(v => v.Conteggio);
        if (resto > 0)
        {
            top.Add(new VocePercentualeResult("Altro", resto, Percentuale(resto, totale)));
        }

        return top;

        static double Percentuale(int conteggio, int totale) => totale > 0 ? Math.Round((double)conteggio / totale * 100, 1) : 0;
    }

    private static IReadOnlyList<ValoreMensileResult> CompletaAiDodiciMesi(IReadOnlyList<(int Mese, decimal Totale)> valori)
    {
        var perMese = valori.ToDictionary(v => v.Mese, v => v.Totale);
        return Enumerable.Range(1, 12).Select(mese => new ValoreMensileResult(mese, perMese.GetValueOrDefault(mese))).ToList();
    }

    private static MatricePrenotazioniTipologiaResult CostruisciMatrice(
        IReadOnlyList<string> tipologie,
        IReadOnlyList<(string Tipologia, int Mese, int Conteggio)> valoriGrezzi)
    {
        var lookup = valoriGrezzi.ToDictionary(v => (v.Tipologia, v.Mese), v => v.Conteggio);
        var righe = Enumerable.Range(1, 12)
            .Select(mese => new RigaMatriceMeseResult(mese, tipologie.Select(t => lookup.GetValueOrDefault((t, mese))).ToList()))
            .ToList();

        return new MatricePrenotazioniTipologiaResult(tipologie, righe);
    }
}
