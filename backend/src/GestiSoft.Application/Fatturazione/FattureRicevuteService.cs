using GestiSoft.Application.Auth;
using GestiSoft.Application.Exceptions;
using GestiSoft.Domain.Entities;

namespace GestiSoft.Application.Fatturazione;

/// <summary>Una fattura ricevuta col nome della struttura a cui si appoggia.</summary>
public record FatturaRicevutaConStruttura(FatturaRicevuta Fattura, string StrutturaNome);

/// <summary>
/// Le fatture dei fornitori arrivate dallo SdI. Sono del Cliente (scelta dell'utente): da ogni sua
/// struttura si vedono quelle di tutte, anche delle strutture disattivate, perché restano documenti
/// fiscali da conservare. Si leggono col permesso di consultare le fatture della struttura da cui si guarda.
/// </summary>
public class FattureRicevuteService(
    IFatturaRicevutaRepository fattureRicevute,
    IStrutturaRepository strutture,
    PermessoStrutturaGuard permessoGuard)
{
    public async Task<IReadOnlyList<FatturaRicevutaConStruttura>> ListaAsync(ICurrentUser currentUser, Guid strutturaId, int? anno, CancellationToken cancellationToken)
    {
        var delCliente = await StruttureDelClienteAsync(currentUser, strutturaId, cancellationToken);
        var elenco = await fattureRicevute.ListAsync(delCliente.Keys, anno, cancellationToken);
        return elenco.Select(f => new FatturaRicevutaConStruttura(f, delCliente[f.StrutturaId])).ToList();
    }

    public async Task<IReadOnlyList<int>> GetAnniDisponibiliAsync(ICurrentUser currentUser, Guid strutturaId, CancellationToken cancellationToken)
    {
        var delCliente = await StruttureDelClienteAsync(currentUser, strutturaId, cancellationToken);
        return await fattureRicevute.ListaAnniAsync(delCliente.Keys, cancellationToken);
    }

    /// <summary>Il file originale. Scaricarlo vuol dire averla aperta: la fattura non è più "nuova".</summary>
    public async Task<(string NomeFile, byte[] File)> FileAsync(ICurrentUser currentUser, Guid strutturaId, Guid fatturaRicevutaId, CancellationToken cancellationToken)
    {
        var fattura = await GetOwnedAsync(currentUser, strutturaId, fatturaRicevutaId, cancellationToken);
        var file = await fattureRicevute.GetFileAsync(fattura.FileRicevutoSdiId, cancellationToken)
            ?? throw new NotFoundException("File della fattura non trovato.");
        await SegnaLettaAsync(fattura, cancellationToken);
        return file;
    }

    public async Task SegnaLettaAsync(ICurrentUser currentUser, Guid strutturaId, Guid fatturaRicevutaId, CancellationToken cancellationToken)
    {
        var fattura = await GetOwnedAsync(currentUser, strutturaId, fatturaRicevutaId, cancellationToken);
        await SegnaLettaAsync(fattura, cancellationToken);
    }

    private async Task SegnaLettaAsync(FatturaRicevuta fattura, CancellationToken cancellationToken)
    {
        if (fattura.LettaAtUtc is null)
        {
            fattura.LettaAtUtc = DateTime.UtcNow;
            await fattureRicevute.SalvaAsync(cancellationToken);
        }
    }

    private async Task<FatturaRicevuta> GetOwnedAsync(ICurrentUser currentUser, Guid strutturaId, Guid fatturaRicevutaId, CancellationToken cancellationToken)
    {
        var delCliente = await StruttureDelClienteAsync(currentUser, strutturaId, cancellationToken);
        return await fattureRicevute.GetAsync(delCliente.Keys, fatturaRicevutaId, cancellationToken)
            ?? throw new NotFoundException("Fattura ricevuta non trovata.");
    }

    /// <summary>Le strutture dello stesso Cliente, col loro nome. Solo quella di partenza se il Cliente non si trova.</summary>
    private async Task<Dictionary<Guid, string>> StruttureDelClienteAsync(ICurrentUser currentUser, Guid strutturaId, CancellationToken cancellationToken)
    {
        await permessoGuard.EnsureAsync(currentUser, strutturaId, p => p.FinanceRead, cancellationToken);

        var struttura = await strutture.GetByIdAsync(strutturaId, cancellationToken) ?? throw new NotFoundException("Struttura non trovata.");
        var elenco = await strutture.ListByClienteAsync(struttura.ClienteId, includiInattive: true, cancellationToken);
        var nomi = elenco.ToDictionary(s => s.Id, s => s.Nome);
        nomi[struttura.Id] = struttura.Nome;
        return nomi;
    }
}
