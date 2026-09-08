namespace GestiSoft.Domain.Enums;

/// <summary>
/// L'Osservatorio Turistico non è un sistema unico nazionale: ogni Regione ha il proprio (o aderisce
/// a una piattaforma condivisa come ROSS1000, usata da diverse Regioni). Un OsservatorioAppartamento
/// dichiara qui a quale sistema/protocollo parla — l'implementazione di IOsservatorioClient da usare
/// viene risolta da IOsservatorioClientResolver in base a questo valore (vedi Infrastructure/Osservatorio).
/// Un solo valore reale per ora (l'unico verificato/con clienti): aggiungere un altro sistema significa
/// scrivere una nuova implementazione di IOsservatorioClient, registrarla con la sua chiave qui sotto,
/// nessun'altra modifica al resto del modulo Osservatorio.
/// </summary>
public enum ProviderOsservatorio
{
    Sicilia = 1,
}
