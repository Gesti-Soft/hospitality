using System.Security.Cryptography;
using System.Text;
using GestiSoft.Application.AlloggiatiWeb;
using GestiSoft.Domain.Entities;
using GestiSoft.Domain.Enums;

namespace GestiSoft.Application.Osservatorio;

/// <summary>
/// Costruisce lo Stay (arrivo o checkout) di un Ospite per l'Osservatorio Turistico — porta
/// BuildStaysPmsDTO di StatePoliceLogic del legacy. Riusa la stessa anagrafica Comune+Stato
/// unificata per Descrizione già introdotta in Fase 6 per Alloggiati Web
/// (<see cref="IAnagraficaAlloggiatiWebRepository"/>): a differenza del legacy, che duplicava la
/// stessa idea con una funzione a parte (GetComuneProvincia, leggermente diversa e meno testata),
/// qui è la stessa istanza di lookup, un solo posto da mantenere.
/// </summary>
public class StayBuilderOsservatorio
{
    private readonly ILookup<string, VoceAnagrafica> _luoghi;
    private readonly ILookup<string, VoceAnagrafica> _tipiAlloggiato;

    public StayBuilderOsservatorio(IReadOnlyList<VoceAnagrafica> luoghi, IReadOnlyList<VoceAnagrafica> tipiAlloggiato)
    {
        _luoghi = luoghi.ToLookup(v => v.Descrizione, StringComparer.OrdinalIgnoreCase);
        _tipiAlloggiato = tipiAlloggiato.ToLookup(v => v.Descrizione, StringComparer.OrdinalIgnoreCase);
    }

    public OsservatorioStayDto CostruisciArrivo(Ospite ospite, string stayId, string guestIdCapofamiglia, IReadOnlyDictionary<Guid, string> guestIdMembri) =>
        Costruisci(ospite, stayId, checkOut: false, guestIdCapofamiglia, guestIdMembri);

    public OsservatorioStayDto CostruisciCheckout(Ospite ospite, string stayId, string guestIdCapofamiglia, IReadOnlyDictionary<Guid, string> guestIdMembri) =>
        Costruisci(ospite, stayId, checkOut: true, guestIdCapofamiglia, guestIdMembri);

    private OsservatorioStayDto Costruisci(Ospite ospite, string stayId, bool checkOut, string guestIdCapofamiglia, IReadOnlyDictionary<Guid, string> guestIdMembri)
    {
        var checkIn = ospite.Prenotazione?.CheckIn ?? DateTime.UtcNow.Date;
        var checkOutData = ospite.Prenotazione?.CheckOut ?? DateTime.UtcNow.Date;
        var nomeCamera = ospite.Prenotazione?.Camera?.Nome ?? string.Empty;
        var tipoCapofamiglia = CodiceTipoAlloggiato(ospite.TipoOspite);

        // Il capofamiglia/ospite singolo non ha un campo PostoLetto proprio (esiste solo su OspiteRiga,
        // i membri aggiuntivi): occupa sempre un letto per definizione, è il primo occupante.
        var guests = new List<OsservatorioGuestDto>
        {
            CostruisciGuest(guestIdCapofamiglia, ospite.DataNascita, ospite.Cittadinanza, ospite.LuogoNascita, ospite.LuogoResidenza, tipoCapofamiglia, ospite.Sesso, ospite.Email, checkIn, checkOutData, checkOut, bedOccupancy: true, nomeCamera),
        };

        // Stessa regola di Alloggiati Web (Fase 6): il tipo dei membri dipende da quello del capofamiglia.
        var tipoMembroDescrizione = string.Equals(ospite.TipoOspite, "CAPO FAMIGLIA", StringComparison.OrdinalIgnoreCase) ? "FAMILIARE" : "MEMBRO GRUPPO";
        var codiceTipoMembro = CodiceTipoAlloggiato(tipoMembroDescrizione);

        foreach (var membro in ospite.Membri)
        {
            if (!guestIdMembri.TryGetValue(membro.Id, out var guestId))
            {
                continue;
            }

            guests.Add(CostruisciGuest(guestId, membro.DataNascita, membro.Cittadinanza, membro.LuogoNascita, membro.LuogoResidenza, codiceTipoMembro, membro.Sesso, null, checkIn, checkOutData, checkOut, membro.PostoLetto ?? true, nomeCamera));
        }

        return new OsservatorioStayDto(stayId, guests);
    }

    private OsservatorioGuestDto CostruisciGuest(
        string guestId, DateTime? dataNascita, string? cittadinanza, string? luogoNascita, string? luogoResidenza,
        int tipoAlloggiato, Sesso? sesso, string? email, DateTime checkIn, DateTime checkOut, bool isCheckout, bool bedOccupancy, string nomeCamera) =>
        new(
            guestId,
            CalcolaEta(dataNascita),
            CodiceLuogo(cittadinanza),
            CodiceLuogo(luogoNascita),
            CodiceLuogo(luogoResidenza),
            tipoAlloggiato,
            sesso.HasValue ? (int)sesso.Value : 0,
            email,
            checkIn,
            checkOut,
            isCheckout,
            bedOccupancy,
            new[] { new OsservatorioRoomDto(nomeCamera, checkIn, checkOut) });

    private int CodiceTipoAlloggiato(string? descrizione)
    {
        var codice = _tipiAlloggiato[descrizione ?? string.Empty].FirstOrDefault()?.Codice;
        return int.TryParse(codice, out var valore) ? valore : 0;
    }

    private string CodiceLuogo(string? descrizione) =>
        _luoghi[ExtractCity(descrizione) ?? string.Empty].FirstOrDefault()?.Codice ?? string.Empty;

    public static int CalcolaEta(DateTime? dataNascita)
    {
        if (dataNascita is not { } dob)
        {
            return 0;
        }

        var oggi = DateTime.UtcNow.Date;
        var eta = oggi.Year - dob.Year;
        if (dob.Date > oggi.AddYears(-eta))
        {
            eta--;
        }

        return eta;
    }

    /// <summary>Id ospite deterministico per la coppia (persona, prenotazione) — ispirato a GenerateNumericGuestId del legacy, che usava l'Id numerico della prenotazione come sale; qui si usa il Guid, il formato dell'id non ha bisogno di corrispondere bit a bit al legacy (è un identificativo tecnico scambiato solo con l'Osservatorio, non un dato mostrato all'utente).</summary>
    public static string GeneraGuestId(string? cognome, string? nome, DateTime? dataNascita, Guid prenotazioneId)
    {
        var raw = $"{(cognome ?? string.Empty).ToUpperInvariant()}_{(nome ?? string.Empty).ToUpperInvariant()}_{dataNascita:yyyyMMdd}_{prenotazioneId}";
        var hash = SHA256.HashData(Encoding.UTF8.GetBytes(raw));
        var numerico = BitConverter.ToInt64(hash, 0);
        return Math.Abs(numerico).ToString();
    }

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
