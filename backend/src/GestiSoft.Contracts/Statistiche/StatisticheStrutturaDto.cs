namespace GestiSoft.Contracts.Statistiche;

public record StatisticheStrutturaDto(
    int Anno,
    StatisticheKpiDto Kpi,
    IReadOnlyList<VoceConteggioDto> PrenotazioniPerAgenzia,
    IReadOnlyList<VocePercentualeDto> PrenotazioniPerNazionalita,
    IReadOnlyList<ValoreMensileDto> AndamentoRicavoMensile,
    IReadOnlyList<VoceImportoDto> RicavoPerTipologiaCamera,
    MatricePrenotazioniTipologiaDto PrenotazioniPerTipologiaMese,
    StatisticheTassaSoggiornoDto TassaSoggiorno);

public record StatisticheKpiDto(int NumeroPrenotazioni, decimal RicavoStimato, decimal RicavoEffettivo, double? PermanenzaMediaNotti, double TassoOccupazionePercentuale);

public record VoceConteggioDto(string Etichetta, int Conteggio);

public record VocePercentualeDto(string Etichetta, int Conteggio, double Percentuale);

public record ValoreMensileDto(int Mese, decimal Valore);

public record VoceImportoDto(string Etichetta, decimal Importo);

public record RigaMatriceMeseDto(int Mese, IReadOnlyList<int> ConteggiPerTipologia);

public record MatricePrenotazioniTipologiaDto(IReadOnlyList<string> Tipologie, IReadOnlyList<RigaMatriceMeseDto> Righe);

public record StatisticheTassaSoggiornoDto(decimal TotaleAnno, IReadOnlyList<ValoreMensileDto> AndamentoMensile);
