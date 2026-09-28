namespace GestiSoft.Application.Wubook;

/// <summary>Una camera di un ordine OTA, già con la sua parte di importo e di ospiti.</summary>
public record CameraOrdineRisolta(int Indice, int IdCameraWubook, decimal Importo, int NumeroOspiti);

/// <summary>
/// Divide un ordine OTA in una prenotazione per camera. Un ordine con più camere (due famiglie, o
/// due camere dello stesso tipo: `rooms` = "45052,45052") prima veniva scartato, perché l'id della
/// camera si leggeva come un numero solo; e con la marcatura "letta" l'OTA non lo rimandava più.
///
/// L'importo è quello dell'ordine intero: si divide in proporzione ai prezzi per notte di ogni camera
/// (`booked_rooms`), in parti uguali se mancano; i centesimi di resto vanno alla prima camera, così la
/// somma torna sempre uguale all'ordine. Gli ospiti vengono da `rooms_occupancies`; se mancano si
/// dividono in parti uguali, almeno uno per camera.
/// </summary>
public static class OrdineOta
{
    public static IReadOnlyList<CameraOrdineRisolta> Suddividi(WubookPrenotazione booking)
    {
        var camere = booking.Camere is { Count: > 0 } lette
            ? lette
            : DaElencoId(booking.CameraIdWubookRaw);

        if (camere.Count == 0)
        {
            throw new InvalidOperationException($"Id camera OTA non numerico: '{booking.CameraIdWubookRaw}'.");
        }

        // Una camera sola: come sempre, tutto l'ordine è suo.
        if (camere.Count == 1)
        {
            return [new CameraOrdineRisolta(0, camere[0].IdCameraWubook, booking.Importo, booking.Adulti + booking.Bambini)];
        }

        var importi = DividiImporto(booking.Importo, camere.Select(c => c.PrezzoNotti).ToList());
        var ospiti = DividiOspiti(Math.Max(booking.Adulti, 0) + Math.Max(booking.Bambini, 0), camere.Select(c => c.Occupazione).ToList());

        return camere.Select((c, i) => new CameraOrdineRisolta(i, c.IdCameraWubook, importi[i], ospiti[i])).ToList();
    }

    /// <summary>Dal solo elenco `rooms` ("45052,45052"); vuoto se anche un solo id non è un numero.</summary>
    private static List<CameraOrdineOta> DaElencoId(string elenco)
    {
        var camere = new List<CameraOrdineOta>();
        foreach (var id in elenco.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            if (!int.TryParse(id, out var valore))
            {
                return [];
            }

            camere.Add(new CameraOrdineOta(valore, 0m, null));
        }

        return camere;
    }

    public static List<decimal> DividiImporto(decimal totale, IReadOnlyList<decimal> pesi)
    {
        var n = pesi.Count;
        var sommaPesi = pesi.Sum();
        var parti = sommaPesi > 0 && pesi.All(p => p >= 0)
            ? pesi.Select(p => Math.Round(totale * p / sommaPesi, 2, MidpointRounding.AwayFromZero)).ToList()
            : Enumerable.Repeat(Math.Round(totale / n, 2, MidpointRounding.AwayFromZero), n).ToList();

        parti[0] += totale - parti.Sum();
        return parti;
    }

    public static List<int> DividiOspiti(int totale, IReadOnlyList<int?> occupazioni)
    {
        if (occupazioni.All(o => o is > 0))
        {
            return occupazioni.Select(o => o!.Value).ToList();
        }

        var n = occupazioni.Count;
        var base_ = Math.Max(totale / n, 1);
        var parti = Enumerable.Repeat(base_, n).ToList();
        parti[0] += Math.Max(totale - base_ * n, 0);
        return parti;
    }
}
