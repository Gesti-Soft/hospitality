using System.Xml.Linq;
using GestiSoft.Application.Fatturazione.Sdi;

namespace GestiSoft.Sdi;

/// <summary>
/// I due servizi SOAP che lo SdI chiama su di noi. Nessun framework SOAP: i messaggi sono pochi e
/// piccoli, e leggerli dal corpo per nome locale regge meglio di un binding sul contratto. Si
/// instrada per nome locale dell'elemento radice, mai per namespace: un namespace indovinato male
/// ha voluto dire, per chi ci è passato prima, uno SdI che riceveva in pratica un 404.
/// </summary>
public static class ServizioSoap
{
    private static readonly XNamespace Soap = "http://schemas.xmlsoap.org/soap/envelope/";

    public record Chiamata(XElement Operazione, string? NomeFile, string? IdentificativoSdi, byte[]? File);

    /// <summary>La prima operazione nel Body della busta, coi campi comuni a tutte. Null se il corpo non è una busta SOAP.</summary>
    public static Chiamata? Leggi(byte[] corpo)
    {
        var busta = LettoreXmlSdi.Radice(corpo);
        var operazione = busta?.Descendants().FirstOrDefault(e => e.Name.LocalName == "Body")?.Elements().FirstOrDefault();
        if (operazione is null)
        {
            return null;
        }

        return new Chiamata(
            operazione,
            LettoreXmlSdi.Figlio(operazione, "NomeFile"),
            LettoreXmlSdi.Figlio(operazione, "IdentificativoSdI"),
            Base64(LettoreXmlSdi.Figlio(operazione, "File")));
    }

    /// <summary>Base64 spezzato su più righe dai client SOAP: Convert lo accetta, gli spazi bianchi li ignora.</summary>
    public static byte[]? Base64(string? testo)
    {
        if (string.IsNullOrWhiteSpace(testo))
        {
            return null;
        }

        try
        {
            return Convert.FromBase64String(testo);
        }
        catch (FormatException)
        {
            return null;
        }
    }

    /// <summary>
    /// La presa in carico di una fattura ricevuta. Si rispecchia la forma della richiesta: radice nel
    /// namespace della radice ricevuta, figli in quello dei figli ricevuti. Lo schema non dichiara
    /// elementFormDefault, quindi i figli sono senza namespace e si scrivono con xmlns="" esplicito:
    /// con Esito nel namespace della radice lo SdI legge un esito nullo ("Err: NULL" sul portale).
    /// </summary>
    public static string RispostaEr01(XElement richiesta)
    {
        var nsRadice = richiesta.Name.Namespace;
        var nsFigli = richiesta.Elements().FirstOrDefault()?.Name.Namespace ?? XNamespace.None;

        var busta = new XElement(Soap + "Envelope",
            new XAttribute(XNamespace.Xmlns + "soapenv", Soap),
            new XElement(Soap + "Body",
                new XElement(nsRadice + "rispostaRiceviFatture",
                    new XElement(nsFigli + "Esito", "ER01"))));
        return "<?xml version=\"1.0\" encoding=\"UTF-8\"?>" + busta.ToString(SaveOptions.DisableFormatting);
    }
}
