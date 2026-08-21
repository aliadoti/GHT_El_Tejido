using System.Globalization;
using System.Text;
using ElTejido.Application.Common;
using ElTejido.Domain.Respuestas;
using ElTejido.Domain.Usuarios;
using DominioEvaluacion = ElTejido.Domain.Evaluacion.Evaluacion;

namespace ElTejido.Application.Respuestas;

/// <summary>Qué se exporta (P-34 §4.5): «descargar» no es una acción, sino tres.</summary>
public enum RecursoExportacion
{
    Ideas,
    Aportes,
    Evaluaciones,
}

public enum FormatoExportacion
{
    Xlsx,
    Csv,
}

/// <summary>Una tabla lista para escribir: encabezados y filas ya convertidos a texto.</summary>
public sealed record TablaExportable(
    string Nombre,
    IReadOnlyList<string> Encabezados,
    IReadOnlyList<IReadOnlyList<string>> Filas);

/// <summary>
/// P-34 §4.5: el archivo abre declarando su alcance —campaña, filtros, orden, total, fecha y quién
/// exportó—. Sin eso, un archivo suelto en un correo no se puede auditar tres semanas después.
/// </summary>
public sealed record HojaFiltrosExportacion(IReadOnlyList<(string Clave, string Valor)> Lineas);

/// <summary>Contenido completo de una exportación: su hoja de alcance y su tabla de datos.</summary>
public sealed record ExportacionTabular(HojaFiltrosExportacion Filtros, TablaExportable Tabla);

/// <summary>
/// P-34 §4.5 (04 §5.8): construcción de las exportaciones de resultados. Es lógica pura —arma filas
/// de texto, no escribe archivos ni consulta nada—, de modo que el formato (xlsx/csv) queda en el
/// adaptador y el alcance (mismo filtro y mismo orden que la pantalla) queda en el endpoint.
/// </summary>
public static class ExportacionResultados
{
    /// <summary>
    /// Tope explícito de filas por exportación (`04 §5.8`). El `csv` se escribe fila por fila, pero el
    /// `xlsx` lo arma la librería en memoria: el tope es la protección real, y por eso es el mismo
    /// para los dos. Excederlo responde `400`, no un archivo que nadie va a abrir.
    /// </summary>
    public const int TopeFilas = 10_000;

    public static RecursoExportacion LeerRecurso(string? valor)
        => (valor ?? string.Empty).Trim().ToLowerInvariant() switch
        {
            "" or "ideas" => RecursoExportacion.Ideas,
            "aportes" => RecursoExportacion.Aportes,
            "evaluaciones" => RecursoExportacion.Evaluaciones,
            _ => throw new ErrorValidacion(
                "El recurso a exportar no es valido.",
                [new DetalleError("recurso", "valor_invalido")]),
        };

    public static FormatoExportacion LeerFormato(string? valor)
        => (valor ?? string.Empty).Trim().ToLowerInvariant() switch
        {
            "" or "xlsx" => FormatoExportacion.Xlsx,
            "csv" => FormatoExportacion.Csv,
            _ => throw new ErrorValidacion(
                "El formato de exportacion no es valido.",
                [new DetalleError("formato", "valor_invalido")]),
        };

    public static bool LeerAnonimizado(string? valor)
    {
        var texto = (valor ?? string.Empty).Trim();
        if (texto.Length == 0)
        {
            return false;
        }

        return bool.TryParse(texto, out var anonimizado)
            ? anonimizado
            : throw new ErrorValidacion(
                "El parametro anonimizado no es valido.",
                [new DetalleError("anonimizado", "valor_invalido")]);
    }

    /// <summary>Falla antes de leer nada más si el alcance pedido no cabe en un archivo útil.</summary>
    public static void VerificarTope(int filas)
    {
        if (filas > TopeFilas)
        {
            throw new ErrorValidacion(
                $"La exportacion tiene {filas} filas y el tope es {TopeFilas}. Acota los filtros.",
                [new DetalleError("recurso", "excede_tope")]);
        }
    }

    /// <summary>Nombre del archivo: campaña, recurso y fecha, sin caracteres que rompan la descarga.</summary>
    public static string NombreArchivo(
        string nombreCampania, RecursoExportacion recurso, FormatoExportacion formato, DateTimeOffset ahora)
    {
        var extension = formato == FormatoExportacion.Csv ? "csv" : "xlsx";
        var fecha = ahora.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
        return $"{Sanitizar(nombreCampania)}_{recurso.ToString().ToLowerInvariant()}_{fecha}.{extension}";
    }

    /// <summary>Nombre de una entrada del ZIP: `U-000042_Marta-Rueda_idea-2.md` (P-34 §4.5).</summary>
    public static string NombreDocumento(IdeaConsolidada idea, Usuario? participante, bool anonimizado)
    {
        var codigo = participante?.CodigoUsuarioLegible ?? Sanitizar(idea.UsuarioId);
        var nombre = anonimizado || participante is null ? null : Sanitizar(participante.Nombre);
        var partes = nombre is null
            ? new[] { codigo, $"idea-{idea.IdeaIndice}" }
            : [codigo, nombre, $"idea-{idea.IdeaIndice}"];
        return string.Join('_', partes) + ".md";
    }

    public static HojaFiltrosExportacion ConstruirHojaFiltros(
        string nombreCampania,
        RecursoExportacion recurso,
        bool anonimizado,
        IReadOnlyList<(string Clave, string Valor)> filtrosAplicados,
        int totalFilas,
        DateTimeOffset ahora,
        string exportadoPor)
    {
        var lineas = new List<(string, string)>
        {
            ("Campaña", nombreCampania),
            ("Recurso", recurso.ToString().ToLowerInvariant()),
            ("Anonimizado", anonimizado ? "sí" : "no"),
        };

        lineas.AddRange(filtrosAplicados.Count == 0
            ? [("Filtros", "sin filtros: la campaña completa")]
            : filtrosAplicados.Select(filtro => ($"Filtro · {filtro.Clave}", filtro.Valor)));

        lineas.Add(("Total de filas", totalFilas.ToString(CultureInfo.InvariantCulture)));
        lineas.Add(("Exportado", ahora.ToString("u", CultureInfo.InvariantCulture)));
        lineas.Add(("Exportado por", exportadoPor));
        return new HojaFiltrosExportacion(lineas);
    }

    public static TablaExportable ConstruirIdeas(
        IReadOnlyList<IdeaConsolidada> ideas,
        IReadOnlyDictionary<string, Usuario> participantes,
        IReadOnlyDictionary<string, DominioEvaluacion> evaluaciones,
        ILookup<string, VersionIdeaConsolidada> versionesPorIdea,
        bool anonimizado)
    {
        var encabezados = new[]
        {
            "Participante", "Código", "Área", "Empresa", "Sede", "Pregunta", "Idea #",
            "Texto vigente", "Confirmada", "Estado", "Calificación", "Versiones", "Aportes",
            "Creada", "Actualizada", "Id",
        };

        var filas = ideas.Select(idea =>
        {
            var participante = participantes.GetValueOrDefault(idea.UsuarioId);
            var versiones = versionesPorIdea[idea.Id].ToArray();
            var vigente = VersionVigente(idea, versiones);
            var evaluacion = evaluaciones.GetValueOrDefault(idea.Id);
            var aportes = versiones
                .SelectMany(version => version.AporteIdsAcumulados)
                .Distinct(StringComparer.Ordinal)
                .Count();

            return (IReadOnlyList<string>)new[]
            {
                NombreVisible(participante, idea.UsuarioId, anonimizado),
                participante?.CodigoUsuarioLegible ?? string.Empty,
                participante?.Area ?? string.Empty,
                participante?.Empresa ?? string.Empty,
                participante?.Sede ?? string.Empty,
                idea.PreguntaId,
                idea.IdeaIndice.ToString(CultureInfo.InvariantCulture),
                vigente?.Texto ?? string.Empty,
                string.IsNullOrWhiteSpace(idea.VersionConfirmadaRef) ? "no" : "sí",
                idea.EstadoResultado?.ToString().ToLowerInvariant() ?? "en curso",
                Numero(evaluacion?.CalificacionTotal),
                versiones.Length.ToString(CultureInfo.InvariantCulture),
                aportes.ToString(CultureInfo.InvariantCulture),
                Fecha(idea.CreadaEn),
                Fecha(idea.ActualizadaEn),
                idea.Id,
            };
        }).ToArray();

        return new TablaExportable("Ideas", encabezados, filas);
    }

    public static TablaExportable ConstruirAportes(
        IReadOnlyList<IdeaConsolidada> ideas,
        IReadOnlyDictionary<string, Usuario> participantes,
        IReadOnlyList<Respuesta> aportes,
        ILookup<string, VersionIdeaConsolidada> versionesPorIdea,
        bool anonimizado)
    {
        var encabezados = new[]
        {
            "Participante", "Código", "Área", "Pregunta", "Idea #", "Tipo de aporte", "Texto",
            "Versión asociada", "Fecha", "Id del aporte", "Id de la idea",
        };

        var ideasPorId = ideas.ToDictionary(idea => idea.Id, StringComparer.Ordinal);

        // La versión asociada es la primera que incorporó el aporte: así se ve qué produjo cada mensaje.
        var versionPorAporte = new Dictionary<string, int>(StringComparer.Ordinal);
        foreach (var version in versionesPorIdea.SelectMany(grupo => grupo).OrderBy(v => v.NumeroVersion))
        {
            foreach (var aporteId in version.AporteNuevoIds)
            {
                versionPorAporte.TryAdd(aporteId, version.NumeroVersion);
            }
        }

        var filas = aportes
            .Where(aporte => aporte.IdeaId is not null && ideasPorId.ContainsKey(aporte.IdeaId))
            .OrderBy(aporte => aporte.Fecha)
            .Select(aporte =>
            {
                var idea = ideasPorId[aporte.IdeaId!];
                var participante = participantes.GetValueOrDefault(idea.UsuarioId);
                return (IReadOnlyList<string>)new[]
                {
                    NombreVisible(participante, idea.UsuarioId, anonimizado),
                    participante?.CodigoUsuarioLegible ?? string.Empty,
                    participante?.Area ?? string.Empty,
                    idea.PreguntaId,
                    idea.IdeaIndice.ToString(CultureInfo.InvariantCulture),
                    aporte.TipoAporte?.ToString().ToLowerInvariant() ?? string.Empty,
                    aporte.Texto,
                    versionPorAporte.TryGetValue(aporte.Id, out var numero)
                        ? numero.ToString(CultureInfo.InvariantCulture)
                        : string.Empty,
                    Fecha(aporte.Fecha),
                    aporte.Id,
                    idea.Id,
                };
            })
            .ToArray();

        return new TablaExportable("Aportes", encabezados, filas);
    }

    public static TablaExportable ConstruirEvaluaciones(
        IReadOnlyList<IdeaConsolidada> ideas,
        IReadOnlyDictionary<string, Usuario> participantes,
        IReadOnlyList<DominioEvaluacion> evaluaciones,
        bool anonimizado)
    {
        var encabezados = new[]
        {
            "Participante", "Código", "Área", "Pregunta", "Idea #", "Calificación total",
            "Calificación por criterio", "Rúbrica", "Versión de rúbrica", "Recomendación", "Temas",
            "Modelo", "Fecha", "Id de la evaluación", "Id de la idea",
        };

        var ideasPorId = ideas.ToDictionary(idea => idea.Id, StringComparer.Ordinal);

        var filas = evaluaciones
            .Where(evaluacion => evaluacion.IdeaId is not null && ideasPorId.ContainsKey(evaluacion.IdeaId))
            .OrderBy(evaluacion => evaluacion.Fecha)
            .Select(evaluacion =>
            {
                var idea = ideasPorId[evaluacion.IdeaId!];
                var participante = participantes.GetValueOrDefault(idea.UsuarioId);
                var porCriterio = string.Join(
                    " · ",
                    evaluacion.CalificacionPorCriterio.Select(criterio =>
                        $"{criterio.Criterio}={Numero(criterio.Puntaje)}"));

                return (IReadOnlyList<string>)new[]
                {
                    NombreVisible(participante, idea.UsuarioId, anonimizado),
                    participante?.CodigoUsuarioLegible ?? string.Empty,
                    participante?.Area ?? string.Empty,
                    idea.PreguntaId,
                    idea.IdeaIndice.ToString(CultureInfo.InvariantCulture),
                    Numero(evaluacion.CalificacionTotal),
                    porCriterio,
                    evaluacion.RubricaRef,
                    evaluacion.VersionRubrica.ToString(CultureInfo.InvariantCulture),
                    evaluacion.Recomendacion.ToString().ToLowerInvariant(),
                    string.Join(", ", evaluacion.Temas),
                    evaluacion.ConfigLlmSnapshot.Modelo,
                    Fecha(evaluacion.Fecha),
                    evaluacion.Id,
                    idea.Id,
                };
            })
            .ToArray();

        return new TablaExportable("Evaluaciones", encabezados, filas);
    }

    /// <summary>
    /// D1: el nombre real puede salir en un archivo interno, pero la casilla de anonimizado existe
    /// desde el primer día. Sin identidad resuelta, la fila lo dice en vez de dejar un id crudo.
    /// </summary>
    private static string NombreVisible(Usuario? participante, string usuarioId, bool anonimizado)
    {
        if (participante is null)
        {
            return $"Participante no identificado ({usuarioId})";
        }

        return anonimizado ? participante.CodigoUsuarioLegible : participante.Nombre;
    }

    private static VersionIdeaConsolidada? VersionVigente(
        IdeaConsolidada idea, IReadOnlyCollection<VersionIdeaConsolidada> versiones)
        => versiones.FirstOrDefault(version => version.Id == idea.VersionConfirmadaRef)
            ?? versiones.FirstOrDefault(version => version.Id == idea.VersionPropuestaRef);

    private static string Numero(decimal? valor)
        => valor?.ToString("0.##", CultureInfo.InvariantCulture) ?? string.Empty;

    private static string Fecha(DateTimeOffset fecha)
        => fecha.ToString("u", CultureInfo.InvariantCulture);

    /// <summary>
    /// Nombres de archivo en ASCII: los acentos viajan mal en `Content-Disposition` y terminan como
    /// mojibake en la descarga, asi que se pierden aqui y no en el navegador del administrador.
    /// </summary>
    private static string Sanitizar(string valor)
    {
        var descompuesto = valor.Trim().Normalize(NormalizationForm.FormD);
        var sinAcentos = descompuesto
            .Where(caracter => CharUnicodeInfo.GetUnicodeCategory(caracter) != UnicodeCategory.NonSpacingMark);
        var limpio = new string(sinAcentos
            .Select(caracter => char.IsLetterOrDigit(caracter) && caracter < 128 ? caracter : '-')
            .ToArray());
        var compacto = string.Join('-', limpio.Split('-', StringSplitOptions.RemoveEmptyEntries));
        return compacto.Length == 0 ? "sin-nombre" : compacto;
    }
}
