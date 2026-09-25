using GestiSoft.Application.Assistenza;
using GestiSoft.Application.Exceptions;
using GestiSoft.Contracts.Assistenza;

namespace GestiSoft.Api.Controllers;

/// <summary>Parti comuni ai due controller dell'assistenza (struttura e Super Admin).</summary>
internal static class AssistenzaMappatura
{
    /// <summary>Tetto della richiesta: il massimo delle foto consentite più il testo e l'intestazione multipart.</summary>
    public const long LimiteRichiestaByte = (long)AssistenzaService.AllegatiMaxPerMessaggio * AssistenzaService.AllegatoMaxByte + 256 * 1024;

    public static TicketDto ToDto(TicketRiepilogo r, bool perStaff)
    {
        var t = r.Ticket;
        var ultimoStaff = t.UltimoMessaggioStaffAtUtc is { } staff && staff >= t.UltimoMessaggioClienteAtUtc;

        return new TicketDto(
            t.Id,
            t.Numero,
            t.StrutturaId,
            r.StrutturaNome,
            r.ClienteRagioneSociale,
            t.Oggetto,
            t.Stato,
            t.CreatedAtUtc,
            ultimoStaff ? t.UltimoMessaggioStaffAtUtc!.Value : t.UltimoMessaggioClienteAtUtc,
            ultimoStaff,
            perStaff ? AssistenzaService.NonLettoDalloStaff(t) : AssistenzaService.NonLettoDallaStruttura(t),
            t.ChiusoAtUtc,
            t.AnonimizzatoAtUtc);
    }

    public static TicketDettaglioDto ToDettaglioDto(TicketRiepilogo r, bool perStaff) => new(
        ToDto(r, perStaff),
        r.Ticket.Messaggi
            .OrderBy(m => m.CreatedAtUtc)
            .Select(m => new TicketMessaggioDto(
                m.Id,
                m.DaStaff,
                AssistenzaService.NomeAutore(m),
                m.Testo,
                m.CreatedAtUtc,
                m.Allegati
                    .OrderBy(a => a.CreatedAtUtc)
                    .Select(a => new TicketAllegatoDto(a.Id, a.NomeFile, a.DimensioneByte, a.EliminatoAtUtc is not null))
                    .ToList()))
            .ToList());

    /// <summary>
    /// Legge le foto in memoria, scartando subito quelle oltre il limite senza copiarle: il servizio
    /// ripete comunque tutti i controlli, qui si evita solo di caricare in RAM file troppo grandi.
    /// </summary>
    public static async Task<IReadOnlyList<NuovoAllegato>> LeggiAllegatiAsync(IReadOnlyList<IFormFile>? file, CancellationToken cancellationToken)
    {
        if (file is null || file.Count == 0)
        {
            return [];
        }

        if (file.Count > AssistenzaService.AllegatiMaxPerMessaggio)
        {
            throw new ConflictException($"Puoi allegare al massimo {AssistenzaService.AllegatiMaxPerMessaggio} foto per messaggio.");
        }

        var allegati = new List<NuovoAllegato>(file.Count);
        foreach (var f in file)
        {
            if (f.Length > AssistenzaService.AllegatoMaxByte)
            {
                throw new ConflictException($"La foto {Path.GetFileName(f.FileName)} supera il limite di {AssistenzaService.AllegatoMaxByte / (1024 * 1024)} MB.");
            }

            using var memoria = new MemoryStream();
            await f.CopyToAsync(memoria, cancellationToken);
            allegati.Add(new NuovoAllegato(f.FileName, memoria.ToArray()));
        }

        return allegati;
    }
}
