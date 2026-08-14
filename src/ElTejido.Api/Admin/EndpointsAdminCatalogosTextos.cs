using System.Text;
using System.Text.Json;
using ElTejido.Application.Common;
using ElTejido.Application.Configuracion;
using ElTejido.Application.Conversacion;
using ElTejido.Domain.Configuracion;

namespace ElTejido.Api.Admin;

internal static class EndpointsAdminCatalogosTextos
{
    public static IEndpointRouteBuilder MapearEndpointsAdminCatalogosTextos(this IEndpointRouteBuilder app)
    {
        var grupo = app.MapGroup("/api/admin/catalogos-textos")
            .AddEndpointFilter<AutorizacionAdminEndpointFilter>();

        grupo.MapGet("", BuscarAsync);
        grupo.MapPost("", CrearAsync);
        grupo.MapPost("/importar", ImportarAsync);
        // DT-P32-02 §4: revisar el archivo completo antes de escribir, y estado real de preparacion.
        grupo.MapPost("/importar/prevalidar", PrevalidarImportacionAsync)
            .WithMetadata(new LecturaSinEfectosAdmin());
        grupo.MapGet("/readiness", ObtenerReadinessAsync);
        grupo.MapPost("/semillas/{idioma}", CrearDesdeSemillaAsync);
        // DT-P32-02 §4: la base curada y la fotografia legacy dejan de compartir una sola ruta.
        grupo.MapPost("/semillas/{idioma}/base", CrearSemillaBaseAsync);
        grupo.MapGet("/semillas/{idioma}/legacy/preview", PrevalidarSemillaLegacyAsync);
        grupo.MapGet("/semillas/{idioma}/legacy/exportar", ExportarSemillaLegacyAsync);
        grupo.MapPost("/semillas/{idioma}/legacy", CrearSemillaLegacyAsync);
        grupo.MapGet("/efectivo", ObtenerEfectivoAsync);
        grupo.MapGet("/{familiaId}/{idioma}/versiones", ListarVersionesAsync);
        grupo.MapPost("/{familiaId}/{idioma}/versiones", CrearVersionAsync);
        grupo.MapPut("/{familiaId}/{idioma}/versiones/{version:int}", ActualizarAsync);
        grupo.MapPost("/{familiaId}/{idioma}/versiones/{version:int}/activar", ActivarAsync);
        grupo.MapGet("/{familiaId}/{idioma}/versiones/{version:int}/exportar", ExportarAsync);

        return app;
    }

    private static async Task<IResult> BuscarAsync(HttpContext context, CancellationToken cancellationToken)
    {
        var idioma = context.Request.Query["idioma"].ToString();
        var estado = ParsearEstado(context.Request.Query["estado"].ToString());
        var items = await Servicio(context).BuscarAsync(idioma, estado, cancellationToken);
        return Results.Ok(items.Select(Mapear).ToArray());
    }

    private static async Task<IResult> ListarVersionesAsync(
        string familiaId,
        string idioma,
        HttpContext context,
        CancellationToken cancellationToken)
    {
        var items = await Servicio(context).ListarVersionesAsync(familiaId, idioma, cancellationToken);
        return Results.Ok(items.Select(Mapear).ToArray());
    }

    private static async Task<IResult> CrearAsync(
        GuardarCatalogoRequest request,
        HttpContext context,
        CancellationToken cancellationToken)
    {
        var creado = await Servicio(context).CrearAsync(
            new SolicitudGuardarCatalogoTextos(
                request.FamiliaId ?? string.Empty,
                request.Idioma ?? string.Empty,
                request.Mensajes ?? new Dictionary<string, string>(),
                request.Frases ?? new Dictionary<string, IReadOnlyCollection<string>>()),
            Actor(context),
            cancellationToken);
        context.Response.Headers.ETag = creado.Etag;
        return Results.Created(
            $"/api/admin/catalogos-textos/{creado.Catalogo.FamiliaId}/{creado.Catalogo.Idioma}/versiones/{creado.Catalogo.Version}",
            Mapear(creado));
    }

    /// <summary>
    /// DT-P32-02 §3.1: importa el JSON editado como version nueva en borrador. Nunca activa ni
    /// sobrescribe; un contenido invalido devuelve `400` con todos los detalles y cero escrituras.
    /// </summary>
    private static async Task<IResult> ImportarAsync(HttpContext context, CancellationToken cancellationToken)
    {
        var solicitud = await LeerEdicionMasivaAsync(context, cancellationToken);
        var creado = await Servicio(context).ImportarMasivoAsync(solicitud, Actor(context), cancellationToken);
        context.Response.Headers.ETag = creado.Etag;
        return Creado(creado);
    }

    /// <summary>
    /// DT-P32-02 §3.3: misma validacion que la importacion real, sin escribir. Un JSON legible con
    /// contenido invalido responde `200` con `valido:false`; malformado o sobre el limite, `400`.
    /// </summary>
    private static async Task<IResult> PrevalidarImportacionAsync(
        HttpContext context,
        CancellationToken cancellationToken)
    {
        var solicitud = await LeerEdicionMasivaAsync(context, cancellationToken);
        var resultado = await Servicio(context).PrevalidarImportacionAsync(
            solicitud,
            Actor(context),
            cancellationToken);
        return Results.Ok(MapearPrevalidacion(resultado));
    }

    private static async Task<IResult> ObtenerReadinessAsync(
        HttpContext context,
        CancellationToken cancellationToken)
    {
        var readiness = await context.RequestServices
            .GetRequiredService<IServicioReadinessCatalogosTextos>()
            .ObtenerAsync(context.Request.Query["idioma"].ToString(), cancellationToken);
        return Results.Ok(new
        {
            gateHabilitado = readiness.GateHabilitado,
            limites = new
            {
                maxFrasesPorGrupo = readiness.MaxFrasesPorGrupo,
                maxBytesImportacionJson = readiness.MaxBytesImportacionJson,
            },
            listo = readiness.Idiomas.All(x => x.Listo),
            idiomas = readiness.Idiomas.Select(x => new
            {
                idioma = x.Idioma,
                listo = x.Listo,
                tieneActivo = x.TieneActivo,
                versionActiva = x.VersionActiva,
                huellaActiva = x.HuellaActiva,
                activaValida = x.ActivaValida,
                problemasActiva = MapearErrores(x.ProblemasActiva),
                tieneBorrador = x.TieneBorrador,
                totalVersiones = x.TotalVersiones,
                semillaBaseDisponible = x.SemillaBaseDisponible,
                legacyValido = x.LegacyValido,
                conteosLegacy = new
                {
                    mensajes = x.ConteosLegacy.Mensajes,
                    gruposFrases = x.ConteosLegacy.GruposFrases,
                    frases = x.ConteosLegacy.Frases,
                },
                problemasLegacy = MapearErrores(x.ProblemasLegacy),
                campaniasBloqueadas = x.CampaniasBloqueadas.Select(campania => new
                {
                    campaniaId = campania.CampaniaId,
                    nombre = campania.Nombre,
                    estado = campania.Estado,
                    motivo = campania.Motivo,
                }).ToArray(),
            }).ToArray(),
        });
    }

    private static async Task<IResult> CrearVersionAsync(
        string familiaId,
        string idioma,
        ContenidoCatalogoRequest? request,
        HttpContext context,
        CancellationToken cancellationToken)
    {
        SolicitudContenidoCatalogoTextos? contenido = request is null
            ? null
            : new SolicitudContenidoCatalogoTextos(
                request.Mensajes ?? new Dictionary<string, string>(),
                request.Frases ?? new Dictionary<string, IReadOnlyCollection<string>>());
        var creado = await Servicio(context).CrearVersionAsync(
            familiaId,
            idioma,
            contenido,
            Actor(context),
            cancellationToken);
        context.Response.Headers.ETag = creado.Etag;
        return Results.Created(
            $"/api/admin/catalogos-textos/{familiaId}/{idioma}/versiones/{creado.Catalogo.Version}",
            Mapear(creado));
    }

    private static async Task<IResult> ActualizarAsync(
        string familiaId,
        string idioma,
        int version,
        ContenidoCatalogoRequest request,
        HttpContext context,
        CancellationToken cancellationToken)
    {
        var actualizado = await Servicio(context).ActualizarBorradorAsync(
            familiaId,
            idioma,
            version,
            new SolicitudContenidoCatalogoTextos(
                request.Mensajes ?? new Dictionary<string, string>(),
                request.Frases ?? new Dictionary<string, IReadOnlyCollection<string>>()),
            RequerirIfMatch(context),
            Actor(context),
            cancellationToken);
        context.Response.Headers.ETag = actualizado.Etag;
        return Results.Ok(Mapear(actualizado));
    }

    private static async Task<IResult> ActivarAsync(
        string familiaId,
        string idioma,
        int version,
        HttpContext context,
        CancellationToken cancellationToken)
    {
        var activo = await Servicio(context).ActivarAsync(
            familiaId,
            idioma,
            version,
            RequerirIfMatch(context),
            Actor(context),
            cancellationToken);
        context.Response.Headers.ETag = activo.Etag;
        return Results.Ok(Mapear(activo));
    }

    private static async Task<IResult> ObtenerEfectivoAsync(
        HttpContext context,
        CancellationToken cancellationToken)
    {
        var idioma = context.Request.Query["idioma"].ToString();
        if (string.IsNullOrWhiteSpace(idioma))
        {
            throw new ErrorValidacion(
                "El idioma es obligatorio.",
                new[] { new DetalleError("idioma", "obligatorio") });
        }

        var efectivo = await context.RequestServices.GetRequiredService<IProveedorTextosConversacion>()
            .PrevisualizarAsync(idioma, cancellationToken);
        return Results.Ok(new
        {
            origen = MapearOrigen(efectivo.Origen),
            catalogo = efectivo.Version is null ? null : Mapear(efectivo.Version),
        });
    }

    private static async Task<IResult> CrearDesdeSemillaAsync(
        string idioma,
        HttpContext context,
        CancellationToken cancellationToken)
    {
        SolicitudGuardarCatalogoTextos solicitud;
        try
        {
            solicitud = CatalogosTextosSemilla.CrearSolicitud(
                idioma,
                context.RequestServices.GetRequiredService<OpcionesConversacion>());
        }
        catch (ArgumentOutOfRangeException)
        {
            throw new ErrorValidacion(
                "El idioma debe ser 'es' o 'en'.",
                new[] { new DetalleError("idioma", "valor_invalido") });
        }

        // Importar crea v1 o una version nueva, siempre borrador. Nunca activa la semilla.
        var creado = await Servicio(context).ImportarAsync(
            solicitud,
            Actor(context),
            cancellationToken);
        context.Response.Headers.ETag = creado.Etag;
        return Results.Created(
            $"/api/admin/catalogos-textos/{creado.Catalogo.FamiliaId}/{creado.Catalogo.Idioma}/versiones/{creado.Catalogo.Version}",
            Mapear(creado));
    }

    /// <summary>DT-P32-02 §2.1: borrador desde la base curada; no lee App Settings.</summary>
    private static async Task<IResult> CrearSemillaBaseAsync(
        string idioma,
        HttpContext context,
        CancellationToken cancellationToken)
    {
        var creado = await Servicio(context).CrearDesdeSemillaAsync(
            SemillaBase(idioma),
            OrigenSemillaCatalogoTextos.Base,
            Actor(context),
            cancellationToken);
        context.Response.Headers.ETag = creado.Etag;
        return Creado(creado);
    }

    /// <summary>
    /// DT-P32-02 §4: prevalida la configuracion legacy efectiva sin persistir. Responde `200` con
    /// `valido:false` cuando el contenido es legible pero incumple reglas, para que el admin pueda
    /// corregirlo; nunca devuelve los textos revisados.
    /// </summary>
    private static async Task<IResult> PrevalidarSemillaLegacyAsync(
        string idioma,
        HttpContext context,
        CancellationToken cancellationToken)
    {
        var resultado = await Servicio(context).PrevalidarSemillaAsync(
            SemillaLegacy(idioma, context),
            OrigenSemillaCatalogoTextos.Legacy,
            Actor(context),
            cancellationToken);
        return Results.Ok(MapearPrevalidacion(resultado));
    }

    /// <summary>
    /// DT-P32-02 §6: descarga la fotografia legacy completa aunque sea invalida, para corregirla
    /// fuera de linea sin perder entradas. No trunca, no persiste y no mezcla valores base.
    /// </summary>
    private static IResult ExportarSemillaLegacyAsync(string idioma, HttpContext context)
    {
        var solicitud = SemillaLegacy(idioma, context);
        return ArchivoEditable(
            solicitud,
            $"catalogo-{solicitud.FamiliaId}-{solicitud.Idioma}-legacy-editable.json");
    }

    /// <summary>DT-P32-02 §4: solo una fotografia legacy valida completa puede crear borrador.</summary>
    private static async Task<IResult> CrearSemillaLegacyAsync(
        string idioma,
        HttpContext context,
        CancellationToken cancellationToken)
    {
        var creado = await Servicio(context).CrearDesdeSemillaAsync(
            SemillaLegacy(idioma, context),
            OrigenSemillaCatalogoTextos.Legacy,
            Actor(context),
            cancellationToken);
        context.Response.Headers.ETag = creado.Etag;
        return Creado(creado);
    }

    /// <summary>
    /// DT-P32-02 §7: valida `Content-Type` y **tamano antes de deserializar**, limita la profundidad a
    /// la forma contractual y traduce el archivo a la solicitud tipada. Los defectos estructurales se
    /// acumulan para que la prevalidacion los devuelva junto con los de contenido.
    /// </summary>
    private static async Task<SolicitudEdicionMasivaCatalogoTextos> LeerEdicionMasivaAsync(
        HttpContext context,
        CancellationToken cancellationToken)
    {
        var limites = context.RequestServices.GetRequiredService<OpcionesCatalogoTextos>().Limites;
        var cuerpo = await LeerCuerpoAcotadoAsync(context, limites.MaxBytesImportacionJson, cancellationToken);
        var errores = new List<DetalleError>();
        var mensajes = new Dictionary<string, string>(StringComparer.Ordinal);
        var frases = new Dictionary<string, IReadOnlyCollection<string>>(StringComparer.Ordinal);
        string familiaId;
        string idioma;

        try
        {
            using var documento = JsonDocument.Parse(cuerpo, new JsonDocumentOptions { MaxDepth = 8 });
            var raiz = documento.RootElement;
            if (raiz.ValueKind != JsonValueKind.Object)
            {
                throw new ErrorValidacion(
                    "El cuerpo debe ser un objeto JSON.",
                    new[] { new DetalleError("body", "json_invalido") });
            }

            var formato = Texto(raiz, "formato") ?? FormatoCatalogoTextos.V1;
            if (!string.Equals(formato, FormatoCatalogoTextos.V1, StringComparison.Ordinal))
            {
                errores.Add(new DetalleError("formato", "no_soportado"));
            }

            familiaId = Texto(raiz, "familiaId") ?? string.Empty;
            idioma = Texto(raiz, "idioma") ?? string.Empty;
            LeerMensajes(raiz, mensajes, errores);
            LeerFrases(raiz, frases, errores);
        }
        catch (JsonException)
        {
            throw new ErrorValidacion(
                "El archivo no es un JSON valido en UTF-8.",
                new[] { new DetalleError("body", "json_invalido") });
        }

        return new SolicitudEdicionMasivaCatalogoTextos(
            new SolicitudGuardarCatalogoTextos(familiaId, idioma, mensajes, frases),
            errores,
            cuerpo.Length,
            Seleccion(context, "familiaId"),
            Seleccion(context, "idioma"));
    }

    private static async Task<byte[]> LeerCuerpoAcotadoAsync(
        HttpContext context,
        int maxBytes,
        CancellationToken cancellationToken)
    {
        var contentType = context.Request.ContentType ?? string.Empty;
        if (!contentType.Contains("application/json", StringComparison.OrdinalIgnoreCase))
        {
            throw new ErrorValidacion(
                "El contenido debe enviarse como application/json.",
                new[] { new DetalleError("Content-Type", "debe_ser_application_json") });
        }

        if (context.Request.ContentLength is long declarado && declarado > maxBytes)
        {
            throw ErrorTamano(maxBytes);
        }

        using var acumulado = new MemoryStream();
        var buffer = new byte[8192];
        int leido;
        while ((leido = await context.Request.Body.ReadAsync(buffer, cancellationToken)) > 0)
        {
            // Tambien acota los envios sin Content-Length (chunked): nunca se materializa mas del techo.
            if (acumulado.Length + leido > maxBytes)
            {
                throw ErrorTamano(maxBytes);
            }

            acumulado.Write(buffer, 0, leido);
        }

        if (acumulado.Length == 0)
        {
            throw new ErrorValidacion(
                "El cuerpo es obligatorio.",
                new[] { new DetalleError("body", "obligatorio") });
        }

        return acumulado.ToArray();
    }

    private static ErrorValidacion ErrorTamano(int maxBytes)
        => new(
            "El archivo excede el tamano permitido.",
            new[] { new DetalleError("body", $"excede_{maxBytes}_bytes") });

    private static void LeerMensajes(
        JsonElement raiz,
        IDictionary<string, string> mensajes,
        ICollection<DetalleError> errores)
    {
        if (!raiz.TryGetProperty("mensajes", out var nodo))
        {
            errores.Add(new DetalleError("mensajes", "obligatorio"));
            return;
        }

        if (nodo.ValueKind != JsonValueKind.Object)
        {
            errores.Add(new DetalleError("mensajes", "tipo_invalido"));
            return;
        }

        foreach (var propiedad in nodo.EnumerateObject())
        {
            if (propiedad.Value.ValueKind == JsonValueKind.String)
            {
                mensajes[propiedad.Name] = propiedad.Value.GetString() ?? string.Empty;
                continue;
            }

            errores.Add(new DetalleError($"mensajes.{propiedad.Name}", "tipo_invalido"));
        }
    }

    private static void LeerFrases(
        JsonElement raiz,
        IDictionary<string, IReadOnlyCollection<string>> frases,
        ICollection<DetalleError> errores)
    {
        if (!raiz.TryGetProperty("frases", out var nodo))
        {
            errores.Add(new DetalleError("frases", "obligatorio"));
            return;
        }

        if (nodo.ValueKind != JsonValueKind.Object)
        {
            errores.Add(new DetalleError("frases", "tipo_invalido"));
            return;
        }

        foreach (var propiedad in nodo.EnumerateObject())
        {
            if (propiedad.Value.ValueKind != JsonValueKind.Array)
            {
                errores.Add(new DetalleError($"frases.{propiedad.Name}", "tipo_invalido"));
                continue;
            }

            var valores = new List<string>();
            var invalido = false;
            foreach (var elemento in propiedad.Value.EnumerateArray())
            {
                if (elemento.ValueKind != JsonValueKind.String)
                {
                    invalido = true;
                    continue;
                }

                valores.Add(elemento.GetString() ?? string.Empty);
            }

            if (invalido)
            {
                errores.Add(new DetalleError($"frases.{propiedad.Name}", "elemento_no_texto"));
            }

            frases[propiedad.Name] = valores;
        }
    }

    private static string? Texto(JsonElement raiz, string propiedad)
        => raiz.TryGetProperty(propiedad, out var valor) && valor.ValueKind == JsonValueKind.String
            ? valor.GetString()
            : null;

    private static string? Seleccion(HttpContext context, string nombre)
    {
        var valor = context.Request.Query[nombre].ToString();
        return string.IsNullOrWhiteSpace(valor) ? null : valor.Trim();
    }

    private static object MapearPrevalidacion(ResultadoPrevalidacionCatalogoTextos resultado)
        => new
        {
            valido = resultado.Valido,
            familiaId = resultado.FamiliaId,
            idioma = resultado.Idioma,
            conteos = new
            {
                mensajes = resultado.Conteos.Mensajes,
                gruposFrases = resultado.Conteos.GruposFrases,
                frases = resultado.Conteos.Frases,
            },
            errores = MapearErrores(resultado.Errores),
        };

    private static object[] MapearErrores(IReadOnlyList<DetalleError> errores)
        => errores.Select(error => new { field = error.Campo, issue = error.Problema }).ToArray();

    private static SolicitudGuardarCatalogoTextos SemillaBase(string idioma)
        => ConstruirSemilla(() => CatalogosTextosSemilla.CrearBase(idioma), idioma);

    private static SolicitudGuardarCatalogoTextos SemillaLegacy(string idioma, HttpContext context)
        => ConstruirSemilla(
            () => CatalogosTextosSemilla.CrearDesdeLegacy(
                idioma,
                context.RequestServices.GetRequiredService<OpcionesConversacion>()),
            idioma);

    private static SolicitudGuardarCatalogoTextos ConstruirSemilla(
        Func<SolicitudGuardarCatalogoTextos> fabrica,
        string idioma)
    {
        try
        {
            return fabrica();
        }
        catch (ArgumentOutOfRangeException)
        {
            throw new ErrorValidacion(
                "El idioma debe ser 'es' o 'en'.",
                new[] { new DetalleError("idioma", "valor_invalido") });
        }
    }

    private static IResult ArchivoEditable(
        SolicitudGuardarCatalogoTextos solicitud,
        string nombreArchivo,
        object? metadatos = null)
    {
        var json = JsonSerializer.Serialize(
            new
            {
                formato = FormatoCatalogoTextos.V1,
                familiaId = solicitud.FamiliaId,
                idioma = solicitud.Idioma,
                mensajes = solicitud.Mensajes,
                frases = solicitud.Frases,
                metadatos,
            },
            new JsonSerializerOptions(JsonSerializerDefaults.Web) { WriteIndented = true });
        return Results.File(Encoding.UTF8.GetBytes(json), "application/json; charset=utf-8", nombreArchivo);
    }

    private static IResult Creado(VersionCatalogoTextos creado)
        => Results.Created(
            $"/api/admin/catalogos-textos/{creado.Catalogo.FamiliaId}/{creado.Catalogo.Idioma}/versiones/{creado.Catalogo.Version}",
            Mapear(creado));

    private static async Task<IResult> ExportarAsync(
        string familiaId,
        string idioma,
        int version,
        HttpContext context,
        CancellationToken cancellationToken)
    {
        var catalogo = await Servicio(context).ObtenerAsync(familiaId, idioma, version, cancellationToken);
        // DT-P32-02 §3.2: la descarga es la forma canonica editable. Los metadatos son informativos y
        // el importador los ignora: la version nueva siempre la numera el servidor.
        return ArchivoEditable(
            new SolicitudGuardarCatalogoTextos(
                catalogo.Catalogo.FamiliaId,
                catalogo.Catalogo.Idioma,
                catalogo.Catalogo.Mensajes,
                catalogo.Catalogo.Frases),
            $"catalogo-{familiaId}-{idioma}-v{version}-editable.json",
            new
            {
                version = catalogo.Catalogo.Version,
                estado = catalogo.Catalogo.Estado.ToString().ToLowerInvariant(),
                huella = catalogo.Catalogo.Huella,
                creadoEn = catalogo.Catalogo.CreadoEn,
                actualizadoEn = catalogo.Catalogo.ActualizadoEn,
                activadoEn = catalogo.Catalogo.ActivadoEn,
            });
    }

    private static object Mapear(VersionCatalogoTextos version)
        => new
        {
            familiaId = version.Catalogo.FamiliaId,
            idioma = version.Catalogo.Idioma,
            version = version.Catalogo.Version,
            estado = version.Catalogo.Estado.ToString().ToLowerInvariant(),
            mensajes = version.Catalogo.Mensajes,
            frases = version.Catalogo.Frases,
            creadoPor = version.Catalogo.CreadoPor,
            aprobadoPor = version.Catalogo.AprobadoPor,
            creadoEn = version.Catalogo.CreadoEn,
            actualizadoEn = version.Catalogo.ActualizadoEn,
            activadoEn = version.Catalogo.ActivadoEn,
            huella = version.Catalogo.Huella,
            etag = version.Etag,
        };

    private static string MapearOrigen(OrigenTextosConversacion origen)
        => origen switch
        {
            OrigenTextosConversacion.Legacy => "legacy",
            OrigenTextosConversacion.Catalogo => "catalogo",
            OrigenTextosConversacion.Cache => "cache",
            OrigenTextosConversacion.UltimaVersionValida => "ultimaVersionValida",
            OrigenTextosConversacion.Emergencia => "emergencia",
            _ => throw new InvalidOperationException($"Origen de textos no soportado: {origen}."),
        };

    private static EstadoCatalogoTextos? ParsearEstado(string? valor)
    {
        if (string.IsNullOrWhiteSpace(valor))
        {
            return null;
        }

        if (Enum.TryParse<EstadoCatalogoTextos>(valor, true, out var estado))
        {
            return estado;
        }

        throw new ErrorValidacion(
            "El estado debe ser borrador, activo o inactivo.",
            new[] { new DetalleError("estado", "valor_invalido") });
    }

    private static string RequerirIfMatch(HttpContext context)
    {
        var etag = context.Request.Headers.IfMatch.ToString();
        if (string.IsNullOrWhiteSpace(etag))
        {
            throw new ErrorValidacion(
                "El header If-Match es obligatorio.",
                new[] { new DetalleError("If-Match", "obligatorio") });
        }

        return etag.Trim();
    }

    private static string Actor(HttpContext context)
        => AutorizacionAdminEndpointFilter.ObtenerPrincipal(context).UsuarioId;

    private static IServicioGestionCatalogosTextos Servicio(HttpContext context)
        => context.RequestServices.GetRequiredService<IServicioGestionCatalogosTextos>();

    private sealed record GuardarCatalogoRequest(
        string? FamiliaId,
        string? Idioma,
        IReadOnlyDictionary<string, string>? Mensajes,
        IReadOnlyDictionary<string, IReadOnlyCollection<string>>? Frases);

    private sealed record ContenidoCatalogoRequest(
        IReadOnlyDictionary<string, string>? Mensajes,
        IReadOnlyDictionary<string, IReadOnlyCollection<string>>? Frases);
}
