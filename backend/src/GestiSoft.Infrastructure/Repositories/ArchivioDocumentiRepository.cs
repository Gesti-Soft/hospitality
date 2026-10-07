using GestiSoft.Application.Fatturazione;
using GestiSoft.Domain.Entities;
using GestiSoft.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace GestiSoft.Infrastructure.Repositories;

public class ArchivioDocumentiRepository(GestiSoftDbContext db) : IArchivioDocumentiRepository
{
    public async Task<IReadOnlyList<DocumentoFiscaleArchiviato>> ListAsync(Guid? clienteId, CancellationToken cancellationToken)
    {
        var query = db.DocumentiFiscaliArchiviati.AsNoTracking();
        if (clienteId is { } id)
        {
            query = query.Where(d => d.ClienteId == id);
        }

        return await query
            .OrderByDescending(d => d.Data)
            .ThenBy(d => d.StrutturaNome)
            // PDF e file restano nel database: si scaricano uno per uno.
            .Select(d => new DocumentoFiscaleArchiviato
            {
                Id = d.Id,
                ClienteId = d.ClienteId,
                ClienteRagioneSociale = d.ClienteRagioneSociale,
                StrutturaIdOriginale = d.StrutturaIdOriginale,
                StrutturaNome = d.StrutturaNome,
                StrutturaPartitaIva = d.StrutturaPartitaIva,
                Tipo = d.Tipo,
                Numero = d.Numero,
                Data = d.Data,
                Controparte = d.Controparte,
                Importo = d.Importo,
                NomeFile = d.NomeFile,
                // Solo per sapere se ci sono, senza portarsi dietro i byte.
                Pdf = d.Pdf == null ? null : Array.Empty<byte>(),
                File = d.File == null ? null : Array.Empty<byte>(),
                ArchiviatoAtUtc = d.ArchiviatoAtUtc,
                Scadenza = d.Scadenza,
            })
            .ToListAsync(cancellationToken);
    }

    public Task<DocumentoFiscaleArchiviato?> GetAsync(Guid id, CancellationToken cancellationToken) =>
        db.DocumentiFiscaliArchiviati.AsNoTracking().FirstOrDefaultAsync(d => d.Id == id, cancellationToken);

    public async Task<IReadOnlyList<DocumentoFiscaleArchiviato>> ListCompletiAsync(Guid clienteId, CancellationToken cancellationToken) =>
        await db.DocumentiFiscaliArchiviati.AsNoTracking()
            .Where(d => d.ClienteId == clienteId)
            .OrderBy(d => d.StrutturaNome).ThenBy(d => d.Data)
            .ToListAsync(cancellationToken);

    public Task<int> EliminaScadutiAsync(DateOnly oggi, CancellationToken cancellationToken) =>
        db.DocumentiFiscaliArchiviati.Where(d => d.Scadenza < oggi).ExecuteDeleteAsync(cancellationToken);
}
