using System.Globalization;
using System.Security.Cryptography;
using System.Text;

namespace ElTejido.Domain.Configuracion;

/// <summary>
/// Compilador <b>determinista</b> de la proyeccion Markdown de una version de rubrica
/// (DT-RUB-01 §2/§3.1, 03 §3.11, 07 §3.1).
/// <para>
/// Es la unica forma de producir <c>contenidoMarkdown</c>: el Markdown dejo de ser una entrada del
/// autor y paso a derivarse de la estructura, de modo que no puede agregar, quitar ni renombrar
/// criterios. La misma estructura produce siempre el mismo texto y la misma huella, sin importar el
/// orden de las propiedades del JSON recibido.
/// </para>
/// </summary>
public static class CompiladorRubricaMarkdown
{
    // Separadores de control (unit/record separator): no pueden aparecer en un texto de autor,
    // asi que la forma canonica no es ambigua ni se puede falsificar concatenando campos.
    private const char SeparadorCampo = '\u001f';
    private const char SeparadorRegistro = '\u001e';

    /// <summary>Compila la proyeccion Markdown. Los criterios se emiten ordenados por <c>Orden</c>.</summary>
    public static string Compilar(
        string nombre,
        string? descripcion,
        string? instruccionesGenerales,
        EscalaRubrica escala,
        IReadOnlyList<CriterioRubrica> criterios)
    {
        var builder = new StringBuilder();
        builder.Append("# Rubrica: ").Append(Linea(nombre)).Append('\n');

        if (!string.IsNullOrWhiteSpace(descripcion))
        {
            builder.Append('\n').Append(Linea(descripcion)).Append('\n');
        }

        if (!string.IsNullOrWhiteSpace(instruccionesGenerales))
        {
            builder.Append("\n## Instrucciones generales\n\n")
                .Append(Linea(instruccionesGenerales))
                .Append('\n');
        }

        builder.Append("\n## Escala\n\n")
            .Append(FormattableString.Invariant(
                $"Cada criterio se califica con un puntaje entre {escala.Min} y {escala.Max}.\n"));

        builder.Append("\n## Criterios\n\n");
        foreach (var criterio in Ordenar(criterios))
        {
            builder.Append(FormattableString.Invariant($"{criterio.Orden}. "))
                .Append("**").Append(Linea(criterio.Nombre)).Append("** ")
                .Append("(id: `").Append(criterio.Id).Append("`, peso ").Append(Peso(criterio.Peso)).Append(')');

            if (!string.IsNullOrWhiteSpace(criterio.Descripcion))
            {
                builder.Append(" - ").Append(Linea(criterio.Descripcion));
            }

            builder.Append('\n');
        }

        return builder.ToString();
    }

    /// <summary>
    /// Huella de integridad sobre la <b>representacion canonica</b> de la version: escala,
    /// instrucciones y criterios en orden con id, nombre, descripcion y peso normalizados. No incluye
    /// nombre de familia, version, estado ni fechas, porque describe la estructura evaluable, no el
    /// documento.
    /// </summary>
    public static string CalcularHuella(
        string? instruccionesGenerales,
        EscalaRubrica escala,
        IReadOnlyList<CriterioRubrica> criterios)
    {
        var canonico = new StringBuilder();
        canonico.Append(FormattableString.Invariant($"escala{SeparadorCampo}{escala.Min}{SeparadorCampo}{escala.Max}"))
            .Append(SeparadorRegistro)
            .Append("instrucciones").Append(SeparadorCampo).Append(Linea(instruccionesGenerales));

        foreach (var criterio in Ordenar(criterios))
        {
            canonico.Append(SeparadorRegistro)
                .Append(FormattableString.Invariant($"{criterio.Orden}"))
                .Append(SeparadorCampo).Append(criterio.Id)
                .Append(SeparadorCampo).Append(NormalizacionRubrica.ClaveComparacion(criterio.Nombre))
                .Append(SeparadorCampo).Append(Linea(criterio.Descripcion))
                .Append(SeparadorCampo).Append(Peso(criterio.Peso));
        }

        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(canonico.ToString()));
        return "sha256:" + Convert.ToHexString(bytes).ToLowerInvariant();
    }

    private static IEnumerable<CriterioRubrica> Ordenar(IReadOnlyList<CriterioRubrica> criterios)
        => criterios.OrderBy(c => c.Orden).ThenBy(c => c.Id, StringComparer.Ordinal);

    /// <summary>
    /// Peso con escala normalizada: <c>0.3m</c> y <c>0.30m</c> tienen el mismo valor pero distinta
    /// representacion, y el Markdown/huella deben coincidir en ambos casos.
    /// </summary>
    private static string Peso(decimal peso) => peso.ToString("0.####", CultureInfo.InvariantCulture);

    /// <summary>
    /// Colapsa saltos de linea y espacios: un texto de autor no puede romper la estructura del
    /// documento compilado ni volver la huella dependiente del formato de entrada.
    /// </summary>
    private static string Linea(string? valor)
    {
        if (string.IsNullOrWhiteSpace(valor))
        {
            return string.Empty;
        }

        var builder = new StringBuilder(valor.Length);
        var espacioPendiente = false;
        foreach (var c in valor.Trim())
        {
            if (char.IsWhiteSpace(c))
            {
                espacioPendiente = builder.Length > 0;
                continue;
            }

            if (espacioPendiente)
            {
                builder.Append(' ');
                espacioPendiente = false;
            }

            builder.Append(c);
        }

        return builder.ToString();
    }
}
