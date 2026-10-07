using System.Xml;
using System.Xml.Linq;
using System.Xml.Schema;

namespace GestiSoft.Infrastructure.Fatturazione;

/// <summary>
/// Validazione contro lo schema ufficiale FatturaPA FPR12 (v1.2.3), con lo schema xmldsig che importa.
/// Gli schemi sono risorse incorporate: nessuna rete, né all'avvio né a ogni fattura. L'import di
/// xmldsig punta a un URL del W3C; il resolver è spento e lo schema di quel namespace è già nel set,
/// quindi l'URL non viene mai aperto.
/// </summary>
internal static class SchemaFatturaPA
{
    private static readonly Lazy<XmlSchemaSet> Schemi = new(Carica);

    public static IReadOnlyList<string> Valida(byte[] xml)
    {
        var errori = new List<string>();
        XDocument documento;
        try
        {
            var impostazioni = new XmlReaderSettings { DtdProcessing = DtdProcessing.Prohibit, XmlResolver = null };
            using var reader = XmlReader.Create(new MemoryStream(xml), impostazioni);
            documento = XDocument.Load(reader, LoadOptions.SetLineInfo);
        }
        catch (XmlException ex)
        {
            return [$"XML non leggibile: {ex.Message}"];
        }

        documento.Validate(Schemi.Value, (sender, e) =>
        {
            var dove = sender is XElement elemento ? $"{elemento.Name.LocalName}: " : sender is XAttribute attributo ? $"{attributo.Name.LocalName}: " : "";
            errori.Add(dove + e.Message);
        });

        return errori;
    }

    private static XmlSchemaSet Carica()
    {
        var set = new XmlSchemaSet { XmlResolver = null };
        // xmldsig dichiara una DTD interna (entità): va letta, ma senza mai risolvere nulla da fuori.
        set.Add(LeggiSchema("xmldsig-core-schema.xsd", DtdProcessing.Parse));
        set.Add(LeggiSchema("Schema_VFPR12_v1.2.3.xsd", DtdProcessing.Prohibit));
        set.Compile();
        return set;
    }

    private static XmlSchema LeggiSchema(string nome, DtdProcessing dtd)
    {
        var risorsa = typeof(SchemaFatturaPA).Assembly.GetManifestResourceNames().Single(n => n.EndsWith(nome, StringComparison.Ordinal));
        using var stream = typeof(SchemaFatturaPA).Assembly.GetManifestResourceStream(risorsa)!;
        using var reader = XmlReader.Create(stream, new XmlReaderSettings { DtdProcessing = dtd, XmlResolver = null });
        return XmlSchema.Read(reader, null)!;
    }
}
