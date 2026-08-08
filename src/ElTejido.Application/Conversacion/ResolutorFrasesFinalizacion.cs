using System.Security.Cryptography;
using System.Text;

namespace ElTejido.Application.Conversacion;

/// <summary>
/// Resuelve las listas configurables de DT-P27-01 antes de que el detector las consuma. Una lista
/// inválida se descarta por completo: nunca se intenta "arreglar" parcialmente una configuración de
/// salida, porque podría dejar activos aliases que el operador no revisó.
/// </summary>
public static class ResolutorFrasesFinalizacion
{
    public const int MaxFrasesPorListaPorDefecto = 20;

    public static ResolucionFrasesFinalizacion Resolver(OpcionesConversacion opciones)
    {
        ArgumentNullException.ThrowIfNull(opciones);

        var maximo = opciones.MaxFrasesFinalizacion > 0
            ? opciones.MaxFrasesFinalizacion
            : MaxFrasesPorListaPorDefecto;
        var version = ResolverVersion(opciones);

        return new ResolucionFrasesFinalizacion(
            ResolverLista(
                "finalizarIdea",
                opciones.FrasesFinalizarIdea,
                DetectorIntencionContinuar.FrasesFinalizarIdeaPorDefecto,
                maximo,
                version),
            ResolverLista(
                "finalizarParticipacion",
                opciones.FrasesFinalizarParticipacion,
                DetectorIntencionContinuar.FrasesFinalizarParticipacionPorDefecto,
                maximo,
                version));
    }

    private static ResolucionListaFrasesFinalizacion ResolverLista(
        string nombre,
        IEnumerable<string>? configuradas,
        IReadOnlyList<string> porDefecto,
        int maximo,
        string version)
    {
        var recibidas = configuradas?.ToArray() ?? Array.Empty<string>();
        if (recibidas.Length == 0)
        {
            return new ResolucionListaFrasesFinalizacion(
                nombre,
                porDefecto,
                OrigenFrasesFinalizacion.Compilada,
                MotivoDescarte: null,
                Version: "compilada");
        }

        var normalizadas = new List<string>(recibidas.Length);
        foreach (var frase in recibidas)
        {
            var normalizada = DetectorIntencionContinuar.Normalizar(frase);
            if (normalizada.Length == 0)
            {
                return Descartar(nombre, porDefecto, "vacio", version);
            }

            normalizadas.Add(normalizada);
        }

        if (normalizadas.Distinct(StringComparer.Ordinal).Count() != normalizadas.Count)
        {
            return Descartar(nombre, porDefecto, "duplicado", version);
        }

        if (normalizadas.Count > maximo)
        {
            return Descartar(nombre, porDefecto, "limite", version);
        }

        return new ResolucionListaFrasesFinalizacion(
            nombre,
            normalizadas,
            OrigenFrasesFinalizacion.Configuracion,
            MotivoDescarte: null,
            Version: version);
    }

    private static ResolucionListaFrasesFinalizacion Descartar(
        string nombre,
        IReadOnlyList<string> porDefecto,
        string motivo,
        string version)
        => new(nombre, porDefecto, OrigenFrasesFinalizacion.Compilada, motivo, version);

    private static string ResolverVersion(OpcionesConversacion opciones)
    {
        if (!string.IsNullOrWhiteSpace(opciones.VersionFrasesFinalizacion))
        {
            return opciones.VersionFrasesFinalizacion.Trim();
        }

        var contenido = string.Join(
            "\u001f",
            new[] { "idea" }
                .Concat((opciones.FrasesFinalizarIdea ?? Array.Empty<string>()).Select(DetectorIntencionContinuar.Normalizar))
                .Concat(new[] { "participacion" })
                .Concat((opciones.FrasesFinalizarParticipacion ?? Array.Empty<string>()).Select(DetectorIntencionContinuar.Normalizar)));
        var hash = SHA256.HashData(Encoding.UTF8.GetBytes(contenido));
        return "config-" + Convert.ToHexString(hash)[..12].ToLowerInvariant();
    }
}

public sealed record ResolucionFrasesFinalizacion(
    ResolucionListaFrasesFinalizacion FinalizarIdea,
    ResolucionListaFrasesFinalizacion FinalizarParticipacion)
{
    public IEnumerable<ResolucionListaFrasesFinalizacion> Listas
    {
        get
        {
            yield return FinalizarIdea;
            yield return FinalizarParticipacion;
        }
    }
}

public sealed record ResolucionListaFrasesFinalizacion(
    string Nombre,
    IReadOnlyList<string> Frases,
    OrigenFrasesFinalizacion Origen,
    string? MotivoDescarte,
    string Version)
{
    public bool FueDescartada => MotivoDescarte is not null;
}

public enum OrigenFrasesFinalizacion
{
    Compilada,
    Configuracion,
}
