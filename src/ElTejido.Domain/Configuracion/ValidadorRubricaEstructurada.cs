namespace ElTejido.Domain.Configuracion;

/// <summary>Un incumplimiento de la estructura de rubrica, con el campo y el motivo estable de 04 §5.5.</summary>
public sealed record ErrorRubrica(string Campo, string Motivo);

/// <summary>Resultado de validar una version de rubrica. Vacio significa valida.</summary>
public sealed record ResultadoValidacionRubrica(IReadOnlyList<ErrorRubrica> Errores)
{
    public bool Valido => Errores.Count == 0;

    public static ResultadoValidacionRubrica Ok { get; } = new(Array.Empty<ErrorRubrica>());
}

/// <summary>
/// Validador <b>puro</b> de la estructura canonica de una version de rubrica (DT-RUB-01 §3.1/§5,
/// 07 §3.1). No depende de Cosmos, HTTP ni reloj: lo comparten la escritura real, la prevalidacion
/// sin escritura y el propio dominio, de modo que el preview del portal y el guardado nunca
/// discrepan.
/// <para>
/// Devuelve <b>todos</b> los motivos encontrados —no corta en el primero— para que el portal pueda
/// marcar cada fila. Un solo motivo basta para rechazar el cuerpo completo: nunca se persiste una
/// version parcial.
/// </para>
/// </summary>
public static class ValidadorRubricaEstructurada
{
    /// <summary>
    /// Techo tecnico de criterios (DT-RUB-01 §3.1). Evita payloads abusivos; no es un numero de
    /// negocio y no limita cuantos criterios tiene funcionalmente una rubrica.
    /// </summary>
    public const int MaxCriterios = 50;

    /// <summary>
    /// Tolerancia al comparar la suma de pesos contra <c>1</c>. Permite repartos como
    /// 0.33 + 0.33 + 0.34 sin abrir la puerta a una suma que cambie el resultado ponderado.
    /// </summary>
    public const decimal ToleranciaSumaPesos = 0.0001m;

    /// <summary>
    /// Resuelve el orden efectivo: si <b>ningun</b> criterio trae orden explicito se asigna por
    /// posicion del arreglo (regla de compatibilidad de 03 §3.11 y forma natural de un cuerpo de API
    /// que ya viene ordenado). Si algunos lo traen y otros no, no se inventa nada: la mezcla se
    /// reporta como <c>orden: no_consecutivo</c>.
    /// </summary>
    public static IReadOnlyList<CriterioRubrica> NormalizarOrden(IEnumerable<CriterioRubrica>? criterios)
    {
        var lista = (criterios ?? Array.Empty<CriterioRubrica>()).ToArray();
        if (lista.Length == 0 || lista.Any(c => c.Orden != 0))
        {
            return lista;
        }

        return lista.Select((c, indice) => c with { Orden = indice + 1 }).ToArray();
    }

    /// <summary>
    /// Valida escala y criterios ya normalizados por <see cref="NormalizarOrden"/>. El llamador
    /// decide como traducir los motivos (400 VALIDATION_ERROR en el API, excepcion de dominio en la
    /// entidad).
    /// </summary>
    public static ResultadoValidacionRubrica Validar(EscalaRubrica escala, IReadOnlyList<CriterioRubrica> criterios)
    {
        var errores = new List<ErrorRubrica>();

        if (escala.Min >= escala.Max)
        {
            errores.Add(new ErrorRubrica("escala", "invalida"));
        }

        if (criterios.Count == 0)
        {
            errores.Add(new ErrorRubrica("criterios", "requerido"));
            return new ResultadoValidacionRubrica(errores);
        }

        if (criterios.Count > MaxCriterios)
        {
            errores.Add(new ErrorRubrica("criterios", "limite_excedido"));
            return new ResultadoValidacionRubrica(errores);
        }

        var idsVistos = new Dictionary<string, int>(StringComparer.Ordinal);
        var nombresVistos = new Dictionary<string, int>(StringComparer.Ordinal);
        var ordenesVistos = new Dictionary<int, int>();

        for (var i = 0; i < criterios.Count; i++)
        {
            var criterio = criterios[i];

            if (string.IsNullOrWhiteSpace(criterio.Id))
            {
                errores.Add(new ErrorRubrica($"criterios.{i}.id", "requerido"));
            }
            else if (!NormalizacionRubrica.EsIdCanonico(criterio.Id))
            {
                errores.Add(new ErrorRubrica($"criterios.{i}.id", "formato_invalido"));
            }
            else if (!idsVistos.TryAdd(criterio.Id, i))
            {
                errores.Add(new ErrorRubrica($"criterios.{i}.id", "duplicado"));
            }

            if (string.IsNullOrWhiteSpace(criterio.Nombre))
            {
                errores.Add(new ErrorRubrica($"criterios.{i}.nombre", "requerido"));
            }
            else if (!nombresVistos.TryAdd(NormalizacionRubrica.ClaveComparacion(criterio.Nombre), i))
            {
                errores.Add(new ErrorRubrica($"criterios.{i}.nombre", "duplicado"));
            }

            if (criterio.Peso <= 0 || criterio.Peso > 1)
            {
                errores.Add(new ErrorRubrica($"criterios.{i}.peso", "fuera_de_rango"));
            }

            if (criterio.Orden <= 0)
            {
                errores.Add(new ErrorRubrica($"criterios.{i}.orden", "no_consecutivo"));
            }
            else if (!ordenesVistos.TryAdd(criterio.Orden, i))
            {
                errores.Add(new ErrorRubrica($"criterios.{i}.orden", "duplicado"));
            }
        }

        // El orden debe ser 1..n exactamente una vez cada uno; los huecos se reportan sobre la
        // posicion que rompe la secuencia y no sobre toda la lista.
        if (ordenesVistos.Count == criterios.Count)
        {
            foreach (var (orden, indice) in ordenesVistos)
            {
                if (orden > criterios.Count)
                {
                    errores.Add(new ErrorRubrica($"criterios.{indice}.orden", "no_consecutivo"));
                }
            }
        }

        var suma = criterios.Sum(c => c.Peso);
        if (Math.Abs(suma - 1m) > ToleranciaSumaPesos)
        {
            errores.Add(new ErrorRubrica("criterios.pesos", "suma_invalida"));
        }

        return new ResultadoValidacionRubrica(errores);
    }
}
