using GestiSoft.Application.Auth;
using GestiSoft.Application.Exceptions;
using GestiSoft.Domain.Entities;
using GestiSoft.Domain.Enums;

namespace GestiSoft.Application.Fatturazione;

public record AggiornaDatiAziendaliRequest(
    string? Iso2,
    string? PIva,
    string? CodiceFiscale,
    string? Denominazione,
    string? Nome,
    string? Cognome,
    RegimeFiscale? RegimeFiscale,
    AliquotaIva? AliquotaIvaDefault,
    NaturaIva? NaturaDefault,
    string? DicituraFattura,
    string? Indirizzo,
    string? NCivico,
    string? Cap,
    string? Comune,
    string? Provincia,
    string? Nazione);

/// <summary>
/// Profilo fiscale emittente della struttura (una sola riga per Struttura) — porta
/// Settings.GetDataAzienda()/AddOrUpdateDataAzienda del legacy.
/// </summary>
public class DatiAziendaliService(IDatiAziendaliRepository repository, PermessoStrutturaGuard permessoGuard)
{
    public async Task<DatiAziendali> GetOrDefaultAsync(ICurrentUser currentUser, Guid strutturaId, CancellationToken cancellationToken)
    {
        await permessoGuard.EnsureAsync(currentUser, strutturaId, p => p.FinanceRead, cancellationToken);

        return await repository.GetByStrutturaIdAsync(strutturaId, cancellationToken)
            ?? new DatiAziendali { StrutturaId = strutturaId };
    }

    public async Task<DatiAziendali> AggiornaAsync(ICurrentUser currentUser, Guid strutturaId, AggiornaDatiAziendaliRequest request, CancellationToken cancellationToken)
    {
        await permessoGuard.EnsureAsync(currentUser, strutturaId, p => p.FinanceWrite, cancellationToken);

        var entity = await repository.GetByStrutturaIdAsync(strutturaId, cancellationToken)
            ?? new DatiAziendali { StrutturaId = strutturaId };

        entity.Iso2 = request.Iso2;
        entity.PIva = request.PIva;
        entity.CodiceFiscale = request.CodiceFiscale;
        entity.Denominazione = request.Denominazione;
        entity.Nome = request.Nome;
        entity.Cognome = request.Cognome;
        entity.RegimeFiscale = request.RegimeFiscale;
        entity.AliquotaIvaDefault = request.AliquotaIvaDefault;
        entity.NaturaDefault = request.NaturaDefault;
        entity.DicituraFattura = request.DicituraFattura;
        entity.Indirizzo = request.Indirizzo;
        entity.NCivico = request.NCivico;
        entity.Cap = request.Cap;
        entity.Comune = request.Comune;
        entity.Provincia = request.Provincia;
        entity.Nazione = request.Nazione;
        entity.UpdatedAtUtc = DateTime.UtcNow;

        await repository.UpsertAsync(entity, cancellationToken);
        return entity;
    }

    /// <summary>
    /// Tetto volutamente basso: in fattura il logo viene stampato alto 45pt, e il browser lo
    /// rimpicciolisce gia' prima di inviarlo. Mezzo megabyte e' molto piu' del necessario, e serve
    /// solo a impedire che finisca nel database la foto da 8 megapixel scelta per sbaglio.
    /// </summary>
    public const int LogoMaxByte = 512 * 1024;

    private const string LogoPng = "image/png";
    private const string LogoJpeg = "image/jpeg";

    public async Task<(byte[] Contenuto, string ContentType)?> GetLogoAsync(ICurrentUser currentUser, Guid strutturaId, CancellationToken cancellationToken)
    {
        await permessoGuard.EnsureAsync(currentUser, strutturaId, p => p.FinanceRead, cancellationToken);

        var entity = await repository.GetByStrutturaIdAsync(strutturaId, cancellationToken);
        if (entity?.Logo is not { Length: > 0 } contenuto)
        {
            return null;
        }

        return (contenuto, entity.LogoContentType ?? LogoPng);
    }

    public async Task<DatiAziendali> AggiornaLogoAsync(ICurrentUser currentUser, Guid strutturaId, byte[] contenuto, CancellationToken cancellationToken)
    {
        await permessoGuard.EnsureAsync(currentUser, strutturaId, p => p.FinanceWrite, cancellationToken);

        if (contenuto.Length == 0)
        {
            throw new ConflictException("Il file del logo e' vuoto.");
        }

        if (contenuto.Length > LogoMaxByte)
        {
            throw new ConflictException($"Il logo supera il limite di {LogoMaxByte / 1024} KB: usa un'immagine piu' leggera.");
        }

        // Il formato si riconosce dai byte reali, non dal content-type dichiarato dal browser: quello
        // lo sceglie chi carica, e un file rinominato .png passerebbe il controllo per poi far
        // fallire la generazione di ogni fattura.
        var contentType = RiconosciFormato(contenuto)
            ?? throw new ConflictException("Formato non riconosciuto: il logo deve essere un'immagine PNG o JPEG.");

        var entity = await repository.GetByStrutturaIdAsync(strutturaId, cancellationToken)
            ?? new DatiAziendali { StrutturaId = strutturaId };

        entity.Logo = contenuto;
        entity.LogoContentType = contentType;
        entity.UpdatedAtUtc = DateTime.UtcNow;

        await repository.UpsertAsync(entity, cancellationToken);
        return entity;
    }

    public async Task RimuoviLogoAsync(ICurrentUser currentUser, Guid strutturaId, CancellationToken cancellationToken)
    {
        await permessoGuard.EnsureAsync(currentUser, strutturaId, p => p.FinanceWrite, cancellationToken);

        var entity = await repository.GetByStrutturaIdAsync(strutturaId, cancellationToken);
        if (entity?.Logo is null)
        {
            return;
        }

        entity.Logo = null;
        entity.LogoContentType = null;
        entity.UpdatedAtUtc = DateTime.UtcNow;

        await repository.UpsertAsync(entity, cancellationToken);
    }

    /// <summary>Firma del file: PNG (89 50 4E 47 0D 0A 1A 0A) e JPEG (FF D8 FF), gli unici due formati che il PDF sa incorporare in modo affidabile.</summary>
    private static string? RiconosciFormato(ReadOnlySpan<byte> contenuto)
    {
        ReadOnlySpan<byte> firmaPng = [0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A];
        if (contenuto.Length >= firmaPng.Length && contenuto[..firmaPng.Length].SequenceEqual(firmaPng))
        {
            return LogoPng;
        }

        if (contenuto.Length >= 3 && contenuto[0] == 0xFF && contenuto[1] == 0xD8 && contenuto[2] == 0xFF)
        {
            return LogoJpeg;
        }

        return null;
    }
}
