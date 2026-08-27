using GestiSoft.Application.Auth;
using GestiSoft.Application.Exceptions;
using GestiSoft.Domain.Entities;

namespace GestiSoft.Application.Camere;

public record CreaCanaleVenditaRequest(string Descrizione);

/// <summary>
/// CRUD dei canali/agenzie di vendita (es. "Booking.com", "Diretto") — porta
/// OrderManagement.Model.BusinesObject.SettingAgenzie del legacy, usato come elenco suggerimenti
/// per il campo Agenzia (testo libero) di Prenotazione.
/// </summary>
public class CanaliVenditaService(ICanaleVenditaRepository canali, PermessoStrutturaGuard permessoGuard)
{
    public async Task<IReadOnlyList<SettingAgenzia>> ListaAsync(ICurrentUser currentUser, Guid strutturaId, CancellationToken cancellationToken)
    {
        await permessoGuard.EnsureAsync(currentUser, strutturaId, p => p.SettingAgency, cancellationToken);
        return await canali.ListByStrutturaAsync(strutturaId, cancellationToken);
    }

    public async Task<SettingAgenzia> CreaAsync(ICurrentUser currentUser, Guid strutturaId, CreaCanaleVenditaRequest request, CancellationToken cancellationToken)
    {
        await permessoGuard.EnsureAsync(currentUser, strutturaId, p => p.SettingAgency, cancellationToken);

        var descrizione = request.Descrizione.Trim();
        if (await canali.ExistsByDescrizioneAsync(strutturaId, descrizione, escludiId: null, cancellationToken))
        {
            throw new ConflictException("Esiste già un canale di vendita con questo nome.");
        }

        var entity = new SettingAgenzia { StrutturaId = strutturaId, Descrizione = descrizione };
        await canali.AddAsync(entity, cancellationToken);
        return entity;
    }

    public async Task<SettingAgenzia> AggiornaAsync(ICurrentUser currentUser, Guid strutturaId, Guid canaleId, CreaCanaleVenditaRequest request, CancellationToken cancellationToken)
    {
        await permessoGuard.EnsureAsync(currentUser, strutturaId, p => p.SettingAgency, cancellationToken);

        var entity = await canali.GetAsync(canaleId, cancellationToken)
            ?? throw new NotFoundException("Canale di vendita non trovato.");
        if (entity.StrutturaId != strutturaId)
        {
            throw new NotFoundException("Canale di vendita non trovato.");
        }

        var descrizione = request.Descrizione.Trim();
        if (await canali.ExistsByDescrizioneAsync(strutturaId, descrizione, escludiId: canaleId, cancellationToken))
        {
            throw new ConflictException("Esiste già un canale di vendita con questo nome.");
        }

        entity.Descrizione = descrizione;
        entity.UpdatedAtUtc = DateTime.UtcNow;

        await canali.UpdateAsync(entity, cancellationToken);
        return entity;
    }

    public async Task EliminaAsync(ICurrentUser currentUser, Guid strutturaId, Guid canaleId, CancellationToken cancellationToken)
    {
        await permessoGuard.EnsureAsync(currentUser, strutturaId, p => p.SettingAgency, cancellationToken);

        var entity = await canali.GetAsync(canaleId, cancellationToken)
            ?? throw new NotFoundException("Canale di vendita non trovato.");
        if (entity.StrutturaId != strutturaId)
        {
            throw new NotFoundException("Canale di vendita non trovato.");
        }

        await canali.DeleteAsync(entity, cancellationToken);
    }
}
