using GestiSoft.Application.AlloggiatiWeb;
using GestiSoft.Domain.Entities;

namespace GestiSoft.Application.PayTourist;

/// <summary>
/// Costruisce la prenotazione (capofamiglia + membri) da inviare a PayTourist — porta 1:1 il ramo
/// automatico di StatePoliceLogic.SendSchedinePayTourist del legacy (non il generatore di export
/// locale manuale, che nel legacy aveva riduzioni disattivate e un bug sull'id riduzione dei
/// membri sempre scartato: qui un solo percorso di costruzione, riusato sia per l'invio che per
/// l'export — vedi PayTouristInvioService — quindi quei problemi non si ripresentano).
/// Riusa la stessa anagrafica Comune+Stato unificata di Alloggiati Web/Osservatorio
/// (<see cref="IAnagraficaAlloggiatiWebRepository"/>).
/// Mappature fedeli al legacy anche dove sembrano un riuso improprio di campo (nessun dato
/// aggiuntivo esiste nel modello Ospite per rappresentarle diversamente):
/// DocumentReleasedByCountry/ResidenceCountry derivano entrambi da Cittadinanza (non esiste un
/// "paese di residenza" distinto), Nationality/BirthCountry derivano da StatoNascita (non da
/// Cittadinanza).
/// </summary>
public class PayTouristDtoBuilder
{
    private readonly ILookup<string, VoceAnagrafica> _luoghi;
    private readonly ILookup<string, VoceDocumentoConTypeId> _documenti;
    private readonly ILookup<string, VoceAnagrafica> _tipiAlloggiato;
    private readonly IReadOnlyList<PayTouristRiduzioneDto> _riduzioni;
    private readonly ILookup<string, PayTouristPortaleDto> _portali;
    private readonly bool _nessunPortaleConfigurato;
    private readonly string? _comuneStruttura;

    public PayTouristDtoBuilder(
        IReadOnlyList<VoceAnagrafica> luoghi,
        IReadOnlyList<VoceDocumentoConTypeId> documenti,
        IReadOnlyList<VoceAnagrafica> tipiAlloggiato,
        IReadOnlyList<PayTouristRiduzioneDto> riduzioni,
        IReadOnlyList<PayTouristPortaleDto> portali,
        string? comuneStruttura)
    {
        _luoghi = luoghi.ToLookup(v => v.Descrizione, StringComparer.OrdinalIgnoreCase);
        _documenti = documenti.ToLookup(v => v.Descrizione, StringComparer.OrdinalIgnoreCase);
        _tipiAlloggiato = tipiAlloggiato.ToLookup(v => v.Descrizione, StringComparer.OrdinalIgnoreCase);
        _riduzioni = riduzioni;
        _portali = portali.ToLookup(p => p.Nome, StringComparer.OrdinalIgnoreCase);
        _nessunPortaleConfigurato = portali.Count == 0;
        _comuneStruttura = ExtractCity(comuneStruttura);
    }

    /// <summary>
    /// Costruisce la prenotazione. Restituisce (null, motivo) solo se il portale online è richiesto
    /// (impostazione PortaleOnlineAttivo) ma l'account PayTourist non ha NESSUN portale configurato
    /// — in quel caso la prenotazione va saltata, stesso comportamento del legacy (che loggava
    /// l'errore e passava alla successiva). Se invece i portali esistono ma nessuno corrisponde al
    /// canale di questa specifica prenotazione (<see cref="Prenotazione.Agenzia"/>, es. "Diretta"),
    /// il legacy NON bloccava l'invio — mandava comunque la prenotazione senza l'arricchimento
    /// portale (StatePoliceLogic.SendSchedinePayTourist: il controllo "portal != null" si limitava a
    /// non valorizzare OnlinePortal/TotalFromOnlinePortal/OnlinePortalReservationId, mai a scartare):
    /// fedele qui, non ogni prenotazione arriva da un portale online.
    /// </summary>
    public (PayTouristReservationDto? Prenotazione, string? MotivoScarto) Costruisci(Ospite ospite, bool portaleOnlineRichiesto)
    {
        var prenotazione = ospite.Prenotazione!;
        var checkIn = prenotazione.CheckIn ?? DateTime.UtcNow.Date;
        var checkOut = prenotazione.CheckOut ?? DateTime.UtcNow.Date;

        int? portaleId = null;
        decimal? totaleDaPortale = null;
        string? idPrenotazionePortale = null;

        if (portaleOnlineRichiesto)
        {
            if (_nessunPortaleConfigurato)
            {
                return (null, "Nessun portale online PayTourist configurato per questo account.");
            }

            var portale = _portali[prenotazione.Agenzia ?? string.Empty].FirstOrDefault();
            if (portale is not null)
            {
                portaleId = portale.Id;
                totaleDaPortale = prenotazione.TotalTax;
                idPrenotazionePortale = prenotazione.NumeroPrenotazione;
            }
        }

        var guests = new List<PayTouristGuestDto> { CostruisciCapofamiglia(ospite, checkIn, checkOut) };

        // Stessa regola di Alloggiati Web (Fase 6) e Osservatorio (Fase 7): il tipo alloggiato dei
        // membri dipende da quello del capofamiglia.
        var tipoMembroDescrizione = string.Equals(ospite.TipoOspite, "CAPO FAMIGLIA", StringComparison.OrdinalIgnoreCase)
            ? "FAMILIARE"
            : "MEMBRO GRUPPO";

        foreach (var membro in ospite.Membri)
        {
            guests.Add(CostruisciMembro(membro, tipoMembroDescrizione, checkIn, checkOut));
        }

        var partnerId = $"reservation_{prenotazione.Id}_{checkIn:yyyy-MM-dd}";
        return (new PayTouristReservationDto(partnerId, checkIn, checkOut, portaleId, totaleDaPortale, idPrenotazionePortale, guests), null);
    }

    private PayTouristGuestDto CostruisciCapofamiglia(Ospite ospite, DateTime checkIn, DateTime checkOut) => new(
        PartnerId: $"ospite_{ospite.Id}_{checkIn:yyyy-MM-dd}",
        Nome: ospite.Nome,
        Cognome: ospite.Cognome,
        Email: ospite.Email,
        CheckIn: checkIn,
        CheckOut: checkOut,
        TypeId: CodiceTipoAlloggiato(ospite.TipoOspite),
        DocumentType: CodiceDocumento(ospite.Documento),
        DocumentNumber: ospite.NumeroDocumento,
        DocumentReleasedByCountry: CodiceLuogo(ospite.Cittadinanza),
        DocumentReleasedByCity: CodiceLuogo(ospite.RilascioDocumento),
        Sex: ospite.Sesso.HasValue ? (int)ospite.Sesso.Value : 1,
        Nationality: CodiceLuogo(ospite.StatoNascita),
        ResidenceCountry: CodiceLuogo(ospite.Cittadinanza),
        ResidenceCity: CodiceLuogo(ospite.LuogoResidenza),
        DateOfBirth: ospite.DataNascita,
        BirthCountry: CodiceLuogo(ospite.StatoNascita),
        BirthCity: CodiceLuogo(ospite.LuogoNascita),
        ReductionId: RiduzioneId(ospite.LuogoResidenza, ospite.EsenteDaTassa));

    /// <summary>I membri non hanno campi Documento/Email/RilascioDocumento nel modello (OspiteRiga) — stessa limitazione del legacy (OspitiRow).</summary>
    private PayTouristGuestDto CostruisciMembro(OspiteRiga membro, string tipoOspiteMembro, DateTime checkIn, DateTime checkOut) => new(
        PartnerId: $"ospite_{membro.Id}_{checkIn:yyyy-MM-dd}",
        Nome: membro.Nome,
        Cognome: membro.Cognome,
        Email: null,
        CheckIn: checkIn,
        CheckOut: checkOut,
        TypeId: CodiceTipoAlloggiato(tipoOspiteMembro),
        DocumentType: null,
        DocumentNumber: null,
        DocumentReleasedByCountry: CodiceLuogo(membro.Cittadinanza),
        DocumentReleasedByCity: 0,
        Sex: membro.Sesso.HasValue ? (int)membro.Sesso.Value : 1,
        Nationality: CodiceLuogo(membro.StatoNascita),
        ResidenceCountry: CodiceLuogo(membro.Cittadinanza),
        ResidenceCity: CodiceLuogo(membro.LuogoResidenza),
        DateOfBirth: membro.DataNascita,
        BirthCountry: CodiceLuogo(membro.StatoNascita),
        BirthCity: CodiceLuogo(membro.LuogoNascita),
        ReductionId: RiduzioneId(membro.LuogoResidenza, membro.EsenteDaTassa));

    /// <summary>
    /// Porta ControlReduction del legacy: riduzione per residenza (comune di residenza uguale al
    /// comune della struttura, riduzione il cui nome contiene "resid") altrimenti riduzione per
    /// esenzione (persona esente da tassa di soggiorno, riduzione il cui nome contiene "esenz").
    /// Match per sottostringa sul nome restituito dall'API PayTourist perché <see cref="PayTouristRiduzioneDto"/>
    /// non ha un codice/tipo strutturato — stesso limite del legacy, non un bug: se PayTourist
    /// rinomina le riduzioni o una struttura ne configura più di una con nome simile, vince la prima
    /// trovata.
    /// </summary>
    private int? RiduzioneId(string? luogoResidenza, bool esenteDaTassa)
    {
        var residenza = ExtractCity(luogoResidenza);
        if (!string.IsNullOrWhiteSpace(residenza) && !string.IsNullOrWhiteSpace(_comuneStruttura)
            && string.Equals(residenza, _comuneStruttura, StringComparison.OrdinalIgnoreCase))
        {
            var riduzioneResidenza = _riduzioni.FirstOrDefault(r => r.Nome.Contains("resid", StringComparison.OrdinalIgnoreCase));
            if (riduzioneResidenza is not null)
            {
                return riduzioneResidenza.Id;
            }
        }

        if (esenteDaTassa)
        {
            var riduzioneEsenzione = _riduzioni.FirstOrDefault(r => r.Nome.Contains("esenz", StringComparison.OrdinalIgnoreCase));
            if (riduzioneEsenzione is not null)
            {
                return riduzioneEsenzione.Id;
            }
        }

        return null;
    }

    private int CodiceTipoAlloggiato(string? descrizione)
    {
        var codice = _tipiAlloggiato[descrizione ?? string.Empty].FirstOrDefault()?.Codice;
        return int.TryParse(codice, out var valore) ? valore : 0;
    }

    private int? CodiceDocumento(string? descrizioneDocumento)
    {
        if (string.IsNullOrWhiteSpace(descrizioneDocumento))
        {
            return null;
        }

        var voce = _documenti[descrizioneDocumento].FirstOrDefault();
        return voce is null ? null : voce.TypeId;
    }

    private int CodiceLuogo(string? descrizione)
    {
        var codice = _luoghi[ExtractCity(descrizione) ?? string.Empty].FirstOrDefault()?.Codice;
        return int.TryParse(codice, out var valore) ? valore : 0;
    }

    /// <summary>I campi luogo sono salvati come "COMUNE (PROVINCIA)" — stesso ExtractCity già usato in SchedinaAlloggiatiWebBuilder/StayBuilderOsservatorio (Fasi 6-7).</summary>
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
