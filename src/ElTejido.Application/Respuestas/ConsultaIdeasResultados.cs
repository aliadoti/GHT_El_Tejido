using System.Globalization;
using System.Text;
using ElTejido.Application.Common;
using ElTejido.Domain.Respuestas;
using ElTejido.Domain.Usuarios;

namespace ElTejido.Application.Respuestas;

/// <summary>Columna por la que se ordena el listado de resultados (P-34 §4.2, 04 §5.8).</summary>
public enum OrdenIdeasResultados
{
    /// <summary>Orden natural de I-19: `preguntaId` → `ideaIndice` → `creadaEn`.</summary>
    Natural,
    Participante,
    Calificacion,
    Creada,
    Actualizada,
    Pregunta,
}

/// <summary>Valores de consulta tal como llegan por query string, sin interpretar.</summary>
public sealed record CriteriosIdeasCrudos(
    string? Q = null,
    string? Area = null,
    string? Empresa = null,
    string? Sede = null,
    string? Desde = null,
    string? Hasta = null,
    string? CalificacionMin = null,
    string? CalificacionMax = null,
    string? Confirmada = null,
    string? Orden = null,
    string? Dir = null);

/// <summary>Criterios ya validados y tipados.</summary>
public sealed record CriteriosIdeas(
    string? Q,
    string? Area,
    string? Empresa,
    string? Sede,
    DateTimeOffset? Desde,
    DateTimeOffset? Hasta,
    decimal? CalificacionMin,
    decimal? CalificacionMax,
    bool? Confirmada,
    OrdenIdeasResultados Orden,
    bool Descendente)
{
    public static CriteriosIdeas Vacios { get; } = new(
        null, null, null, null, null, null, null, null, null, OrdenIdeasResultados.Natural, false);

    /// <summary>El texto de la versión vigente solo se necesita cuando hay búsqueda libre.</summary>
    public bool NecesitaTexto => Q is not null;

    /// <summary>La calificación vigente se necesita para filtrarla o para ordenar por ella.</summary>
    public bool NecesitaCalificacion =>
        CalificacionMin is not null
        || CalificacionMax is not null
        || Orden == OrdenIdeasResultados.Calificacion;

    /// <summary>Un filtro del participante obliga a resolver la identidad antes de paginar.</summary>
    public bool FiltraParticipante => Q is not null || Area is not null || Empresa is not null || Sede is not null;
}

/// <summary>
/// P-34 §4.1/§4.2 (04 §5.8): interpretación y aplicación de los filtros y el orden del listado de
/// resultados. Es lógica pura —sin E/S ni HTTP— para que el endpoint solo traduzca la consulta y el
/// repositorio solo lea. Los filtros de I-19 (`usuarioId`, `preguntaId`, estados) siguen resueltos en
/// el endpoint: no cambian y no dependen de la identidad ni de la evaluación.
/// </summary>
public static class ConsultaIdeasResultados
{
    /// <summary>
    /// Valida y tipa la consulta. Devuelve <b>todos</b> los motivos de una vez —como el validador de
    /// rúbricas— para que el portal pueda marcar cada campo, y falla en vez de devolver una lista
    /// vacía: un rango mal escrito no es «no hay resultados».
    /// </summary>
    public static CriteriosIdeas Interpretar(CriteriosIdeasCrudos crudos)
    {
        var errores = new List<DetalleError>();

        var desde = LeerFecha(crudos.Desde, "desde", errores);
        var hasta = LeerFecha(crudos.Hasta, "hasta", errores);
        if (desde is not null && hasta is not null && desde > hasta)
        {
            errores.Add(new DetalleError("desde", "rango_invalido"));
        }

        var calificacionMin = LeerDecimal(crudos.CalificacionMin, "calificacionMin", errores);
        var calificacionMax = LeerDecimal(crudos.CalificacionMax, "calificacionMax", errores);
        if (calificacionMin is not null && calificacionMax is not null && calificacionMin > calificacionMax)
        {
            errores.Add(new DetalleError("calificacionMin", "rango_invalido"));
        }

        var confirmada = LeerBooleano(crudos.Confirmada, "confirmada", errores);
        var orden = LeerOrden(crudos.Orden, errores);
        var descendente = LeerDireccion(crudos.Dir, errores);

        if (errores.Count > 0)
        {
            throw new ErrorValidacion("Los criterios de consulta no son validos.", errores);
        }

        return new CriteriosIdeas(
            Normalizar(crudos.Q),
            Recortar(crudos.Area),
            Recortar(crudos.Empresa),
            Recortar(crudos.Sede),
            desde,
            hasta,
            calificacionMin,
            calificacionMax,
            confirmada,
            orden,
            descendente);
    }

    /// <summary>
    /// Aplica los filtros nuevos y el orden sobre el conjunto <b>ya filtrado</b> por los criterios de
    /// I-19. El orden se resuelve sobre todo el conjunto —nunca sobre una página— y desempata por el
    /// orden natural para que la paginación sea estable.
    /// </summary>
    public static IReadOnlyList<IdeaConsolidada> FiltrarYOrdenar(
        IEnumerable<IdeaConsolidada> ideas,
        CriteriosIdeas criterios,
        IReadOnlyDictionary<string, Usuario> participantes,
        IReadOnlyDictionary<string, string> textosPorIdea,
        IReadOnlyDictionary<string, decimal> calificacionesPorIdea)
    {
        var filtradas = ideas.Where(idea =>
            CoincideParticipante(idea, criterios, participantes)
            && CoincideFecha(idea, criterios)
            && CoincideConfirmada(idea, criterios)
            && CoincideCalificacion(idea, criterios, calificacionesPorIdea)
            && CoincideBusqueda(idea, criterios, participantes, textosPorIdea));

        var natural = filtradas
            .OrderBy(idea => idea.PreguntaId, StringComparer.Ordinal)
            .ThenBy(idea => idea.IdeaIndice)
            .ThenBy(idea => idea.CreadaEn);

        if (criterios.Orden == OrdenIdeasResultados.Natural)
        {
            return natural.ToArray();
        }

        // Las filas sin dato (participante no resuelto, idea sin evaluación) van al final en ambas
        // direcciones: invertir el orden no debe empujarlas al principio de la primera página.
        var ordenadas = criterios.Orden switch
        {
            OrdenIdeasResultados.Participante => OrdenarPor(
                natural,
                criterios.Descendente,
                idea => participantes.TryGetValue(idea.UsuarioId, out var usuario) ? usuario.Nombre : null,
                ComparadorTexto),
            OrdenIdeasResultados.Calificacion => OrdenarPor(
                natural,
                criterios.Descendente,
                idea => calificacionesPorIdea.TryGetValue(idea.Id, out var calificacion) ? calificacion : (decimal?)null,
                Comparer<decimal?>.Default),
            OrdenIdeasResultados.Creada => OrdenarPor(
                natural, criterios.Descendente, idea => (DateTimeOffset?)idea.CreadaEn, Comparer<DateTimeOffset?>.Default),
            OrdenIdeasResultados.Actualizada => OrdenarPor(
                natural, criterios.Descendente, idea => (DateTimeOffset?)idea.ActualizadaEn, Comparer<DateTimeOffset?>.Default),
            OrdenIdeasResultados.Pregunta => OrdenarPor(
                natural, criterios.Descendente, idea => idea.PreguntaId, ComparadorTexto),
            _ => natural,
        };

        return ordenadas.ToArray();
    }

    /// <summary>
    /// Normalización compartida de la búsqueda libre: minúsculas, sin acentos y sin puntuación, de
    /// modo que «Ana Pérez», «ana perez» y «U-000042» se comparen como el usuario los escribiría.
    /// </summary>
    public static string? Normalizar(string? valor)
    {
        if (string.IsNullOrWhiteSpace(valor))
        {
            return null;
        }

        var descompuesto = valor.Trim().ToLowerInvariant().Normalize(NormalizationForm.FormD);
        var limpio = new StringBuilder(descompuesto.Length);
        foreach (var caracter in descompuesto)
        {
            if (CharUnicodeInfo.GetUnicodeCategory(caracter) == UnicodeCategory.NonSpacingMark)
            {
                continue;
            }

            limpio.Append(char.IsLetterOrDigit(caracter) ? caracter : ' ');
        }

        var compacto = string.Join(' ', limpio.ToString().Split(' ', StringSplitOptions.RemoveEmptyEntries));
        return compacto.Length == 0 ? null : compacto;
    }

    /// <summary>Comparación de texto insensible a mayúsculas, con los nulos tolerados.</summary>
    private static IComparer<string?> ComparadorTexto { get; } =
        Comparer<string?>.Create((izquierda, derecha) =>
            string.Compare(izquierda, derecha, StringComparison.OrdinalIgnoreCase));

    /// <summary>
    /// Ordena por la clave pedida sobre el orden natural —que queda como desempate estable, porque
    /// `OrderBy` es estable— y deja las filas sin dato al final en ambas direcciones.
    /// </summary>
    private static IEnumerable<IdeaConsolidada> OrdenarPor<TClave>(
        IOrderedEnumerable<IdeaConsolidada> natural,
        bool descendente,
        Func<IdeaConsolidada, TClave?> clave,
        IComparer<TClave?> comparador)
    {
        var conDatoPrimero = natural.OrderBy(idea => clave(idea) is null);
        return descendente
            ? conDatoPrimero.ThenByDescending(clave, comparador)
            : conDatoPrimero.ThenBy(clave, comparador);
    }

    private static bool CoincideParticipante(
        IdeaConsolidada idea, CriteriosIdeas criterios, IReadOnlyDictionary<string, Usuario> participantes)
    {
        if (criterios.Area is null && criterios.Empresa is null && criterios.Sede is null)
        {
            return true;
        }

        // Sin identidad resuelta no se puede afirmar que la idea pertenezca a un área: no coincide.
        if (!participantes.TryGetValue(idea.UsuarioId, out var usuario))
        {
            return false;
        }

        return CoincideAtributo(criterios.Area, usuario.Area)
            && CoincideAtributo(criterios.Empresa, usuario.Empresa)
            && CoincideAtributo(criterios.Sede, usuario.Sede);
    }

    private static bool CoincideAtributo(string? filtro, string? valor)
        => filtro is null || string.Equals(filtro, valor, StringComparison.OrdinalIgnoreCase);

    private static bool CoincideFecha(IdeaConsolidada idea, CriteriosIdeas criterios)
        => (criterios.Desde is null || idea.CreadaEn >= criterios.Desde)
            && (criterios.Hasta is null || idea.CreadaEn <= criterios.Hasta);

    private static bool CoincideConfirmada(IdeaConsolidada idea, CriteriosIdeas criterios)
        => criterios.Confirmada is null
            || criterios.Confirmada == !string.IsNullOrWhiteSpace(idea.VersionConfirmadaRef);

    private static bool CoincideCalificacion(
        IdeaConsolidada idea, CriteriosIdeas criterios, IReadOnlyDictionary<string, decimal> calificaciones)
    {
        if (criterios.CalificacionMin is null && criterios.CalificacionMax is null)
        {
            return true;
        }

        // Una idea sin evaluación vigente no tiene número que comparar: queda fuera del rango.
        if (!calificaciones.TryGetValue(idea.Id, out var calificacion))
        {
            return false;
        }

        return (criterios.CalificacionMin is null || calificacion >= criterios.CalificacionMin)
            && (criterios.CalificacionMax is null || calificacion <= criterios.CalificacionMax);
    }

    private static bool CoincideBusqueda(
        IdeaConsolidada idea,
        CriteriosIdeas criterios,
        IReadOnlyDictionary<string, Usuario> participantes,
        IReadOnlyDictionary<string, string> textosPorIdea)
    {
        if (criterios.Q is null)
        {
            return true;
        }

        if (participantes.TryGetValue(idea.UsuarioId, out var usuario))
        {
            if (Contiene(usuario.Nombre, criterios.Q) || Contiene(usuario.CodigoUsuarioLegible, criterios.Q))
            {
                return true;
            }
        }

        return textosPorIdea.TryGetValue(idea.Id, out var texto) && Contiene(texto, criterios.Q);
    }

    private static bool Contiene(string? valor, string busquedaNormalizada)
    {
        var normalizado = Normalizar(valor);
        return normalizado is not null && normalizado.Contains(busquedaNormalizada, StringComparison.Ordinal);
    }

    private static string? Recortar(string? valor)
        => string.IsNullOrWhiteSpace(valor) ? null : valor.Trim();

    private static DateTimeOffset? LeerFecha(string? valor, string campo, List<DetalleError> errores)
    {
        var texto = Recortar(valor);
        if (texto is null)
        {
            return null;
        }

        if (!DateTimeOffset.TryParse(
                texto,
                CultureInfo.InvariantCulture,
                DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal,
                out var fecha))
        {
            errores.Add(new DetalleError(campo, "formato_invalido"));
            return null;
        }

        return fecha;
    }

    private static decimal? LeerDecimal(string? valor, string campo, List<DetalleError> errores)
    {
        var texto = Recortar(valor);
        if (texto is null)
        {
            return null;
        }

        if (!decimal.TryParse(texto, NumberStyles.Number, CultureInfo.InvariantCulture, out var numero))
        {
            errores.Add(new DetalleError(campo, "formato_invalido"));
            return null;
        }

        return numero;
    }

    private static bool? LeerBooleano(string? valor, string campo, List<DetalleError> errores)
    {
        var texto = Recortar(valor);
        if (texto is null)
        {
            return null;
        }

        if (!bool.TryParse(texto, out var booleano))
        {
            errores.Add(new DetalleError(campo, "valor_invalido"));
            return null;
        }

        return booleano;
    }

    private static OrdenIdeasResultados LeerOrden(string? valor, List<DetalleError> errores)
    {
        var texto = Recortar(valor)?.ToLowerInvariant();
        return texto switch
        {
            null => OrdenIdeasResultados.Natural,
            "participante" => OrdenIdeasResultados.Participante,
            "calificacion" => OrdenIdeasResultados.Calificacion,
            "creada" => OrdenIdeasResultados.Creada,
            "actualizada" => OrdenIdeasResultados.Actualizada,
            "pregunta" => OrdenIdeasResultados.Pregunta,
            _ => Invalido(errores),
        };

        static OrdenIdeasResultados Invalido(List<DetalleError> errores)
        {
            errores.Add(new DetalleError("orden", "valor_invalido"));
            return OrdenIdeasResultados.Natural;
        }
    }

    private static bool LeerDireccion(string? valor, List<DetalleError> errores)
    {
        var texto = Recortar(valor)?.ToLowerInvariant();
        switch (texto)
        {
            case null:
            case "asc":
                return false;
            case "desc":
                return true;
            default:
                errores.Add(new DetalleError("dir", "valor_invalido"));
                return false;
        }
    }
}
