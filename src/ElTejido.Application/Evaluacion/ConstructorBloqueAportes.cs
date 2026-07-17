using System.Globalization;
using ElTejido.Application.Conversacion;

namespace ElTejido.Application.Evaluacion;

/// <summary>
/// Arma el bloque de <c>APORTES_DE_LA_COMUNIDAD</c> (I-09, 08 §3.2/§5.9) a partir de los aportes
/// recuperados: <b>sanitiza</b> cada fragmento (neutraliza patrones de inyección transitiva), lo
/// formatea como línea de dato y aplica un <b>presupuesto de tokens</b> truncando antes de armar el
/// prompt. El bloque resultante lo inyecta <see cref="ConstructorMensajesEvaluacion"/> dentro del
/// delimitador de dato no confiable — nunca como instrucción. Si tras las guardas no queda ninguna
/// línea, el bloque se omite por completo.
/// </summary>
public static class ConstructorBloqueAportes
{
    public static BloqueAportes Construir(IReadOnlyList<AporteRelevante> aportes, int presupuestoTokens)
    {
        if (aportes is null || aportes.Count == 0 || presupuestoTokens <= 0)
        {
            return BloqueAportes.Vacio;
        }

        var lineas = new List<string>(aportes.Count);
        var inyeccionSospechosa = false;
        var tokensUsados = 0;

        foreach (var aporte in aportes)
        {
            var limpio = SanitizadorAportes.NeutralizarInstrucciones(aporte.Resumen, out var detectada);
            inyeccionSospechosa |= detectada;
            if (string.IsNullOrWhiteSpace(limpio))
            {
                continue;
            }

            var linea = FormatearLinea(limpio, aporte);
            var tokensLinea = SanitizadorAportes.EstimarTokens(linea);
            if (tokensUsados + tokensLinea > presupuestoTokens)
            {
                // Presupuesto agotado: se trunca el bloque aquí (los aportes vienen ordenados por
                // relevancia, así que se conservan los más relevantes).
                break;
            }

            tokensUsados += tokensLinea;
            lineas.Add(linea);
        }

        return new BloqueAportes(lineas, inyeccionSospechosa);
    }

    private static string FormatearLinea(string resumen, AporteRelevante aporte)
    {
        var tags = aporte.Tags.Count == 0 ? "-" : string.Join(", ", aporte.Tags);
        var fecha = aporte.Fecha.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
        return $"- {resumen}  [tags: {tags}; fecha: {fecha}]";
    }
}

/// <summary>
/// Resultado del armado del bloque: las <see cref="Lineas"/> de dato ya sanitizadas y presupuestadas
/// (vacío = se omite el bloque) y <see cref="InyeccionSospechosa"/> = true si algún fragmento contenía
/// un intento de inyección (para registrar <c>PromptInjectionSospechoso</c>, 08 §5.9).
/// </summary>
public sealed record BloqueAportes(IReadOnlyList<string> Lineas, bool InyeccionSospechosa)
{
    public static readonly BloqueAportes Vacio = new(Array.Empty<string>(), false);

    public bool TieneAportes => Lineas.Count > 0;
}
