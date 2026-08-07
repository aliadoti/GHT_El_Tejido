using ElTejido.Application.Common;
using ElTejido.Application.Configuracion;
using ElTejido.Application.Seguridad;
using ElTejido.Domain.Common;
using ElTejido.Domain.Identidad;
using ElTejido.Domain.Seguridad;
using ElTejido.Domain.Usuarios;

namespace ElTejido.Application.Usuarios.CargaMasiva;

/// <summary>
/// Implementacion de la carga masiva con la plantilla oficial de GHT (I-08 §4). Trabaja en dos pasadas:
/// <list type="number">
/// <item>
/// <b>Planear</b> — valida cada fila y decide que le pasaria (crear, actualizar, reasignar o rechazar
/// con motivo tipificado) <b>sin escribir nada</b>. Asi una fila en conflicto no deja a medias al resto
/// y se puede saber cuantas altas habra.
/// </item>
/// <item>
/// <b>Ejecutar</b> — reserva de una sola vez exactamente tantos codigos como altas (03 §3.1.1) y aplica
/// los cambios en orden de fila.
/// </item>
/// </list>
/// La operacion queda auditada en <see cref="IRepositorioLogSeguridad"/> con conteos y correlationId,
/// sin PII.
/// </summary>
public sealed class ServicioCargaMasiva : IServicioCargaMasiva
{
    private const string TipoTagEmpresa = "empresa";

    private readonly IReadOnlyList<ILectorArchivoParticipantes> _lectores;
    private readonly IRepositorioUsuarios _usuarios;
    private readonly INormalizadorNumero _normalizador;
    private readonly IServicioGestionCampanias _campanias;
    private readonly IRepositorioLogSeguridad _logSeguridad;
    private readonly IProveedorCorrelacion _correlacion;
    private readonly TimeProvider _tiempo;

    public ServicioCargaMasiva(
        IEnumerable<ILectorArchivoParticipantes> lectores,
        IRepositorioUsuarios usuarios,
        INormalizadorNumero normalizador,
        IServicioGestionCampanias campanias,
        IRepositorioLogSeguridad logSeguridad,
        IProveedorCorrelacion correlacion,
        TimeProvider tiempo)
    {
        _lectores = lectores.ToArray();
        _usuarios = usuarios;
        _normalizador = normalizador;
        _campanias = campanias;
        _logSeguridad = logSeguridad;
        _correlacion = correlacion;
        _tiempo = tiempo;
    }

    public async Task<ReporteCargaMasiva> CargarAsync(
        string nombreArchivo,
        Stream contenido,
        string? campaniaId,
        string modo,
        IReadOnlyCollection<ResolucionConflictoTitular> resoluciones,
        CancellationToken cancellationToken)
    {
        var modoNormalizado = NormalizarModo(modo);
        var decisiones = NormalizarResoluciones(resoluciones);

        var lector = ResolverLector(nombreArchivo);
        var filas = await lector.LeerAsync(contenido, cancellationToken);

        var planes = await PlanearAsync(filas, modoNormalizado, decisiones, cancellationToken);
        var resultados = await EjecutarAsync(planes, cancellationToken);

        var idsParaAsociar = resultados
            .Where(r => r.Resultado != ResultadoCarga.Rechazado && r.UsuarioId is not null)
            .Select(r => r.UsuarioId!)
            .ToArray();
        var asociados = await AsociarSiCorrespondeAsync(campaniaId, idsParaAsociar, cancellationToken);

        var reporte = new ReporteCargaMasiva(
            filas.Count,
            resultados.Count(r => r.Resultado == ResultadoCarga.Creado),
            resultados.Count(r => r.Resultado == ResultadoCarga.Actualizado),
            resultados.Count(r => r.Resultado == ResultadoCarga.Reasignado),
            resultados.Count(r => r.Resultado == ResultadoCarga.Rechazado),
            asociados,
            resultados);

        await AuditarAsync(campaniaId, modoNormalizado, reporte, cancellationToken);
        return reporte;
    }

    // ---------- Pasada 1: planear (sin escribir) ----------

    private async Task<IReadOnlyList<PlanFila>> PlanearAsync(
        IReadOnlyList<FilaParticipanteCarga> filas,
        string modo,
        IReadOnlyDictionary<int, string> decisiones,
        CancellationToken cancellationToken)
    {
        // Foto de los activos al inicio del lote: el maestro es pequeno y esto evita una consulta por
        // fila para el telefono y otra para el email.
        var activos = await _usuarios.BuscarUsuariosAsync(
            new FiltroUsuarios(null, EstadoRegistro.Activo, null, null, [], null),
            cancellationToken);

        var porNumero = activos
            .GroupBy(u => u.WhatsappNormalizado.Valor, StringComparer.Ordinal)
            .ToDictionary(g => g.Key, g => g.First(), StringComparer.Ordinal);
        var emailesTomados = activos
            .Where(u => u.Email is not null)
            .ToDictionary(u => u.Email!, u => u.Id, StringComparer.OrdinalIgnoreCase);

        var numerosVistos = new HashSet<string>(StringComparer.Ordinal);
        var emailesEnArchivo = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var planes = new List<PlanFila>(filas.Count);

        foreach (var fila in filas)
        {
            cancellationToken.ThrowIfCancellationRequested();
            planes.Add(PlanearFila(fila, modo, decisiones, porNumero, emailesTomados, numerosVistos, emailesEnArchivo));
        }

        return planes;
    }

    private PlanFila PlanearFila(
        FilaParticipanteCarga fila,
        string modo,
        IReadOnlyDictionary<int, string> decisiones,
        IReadOnlyDictionary<string, Usuario> porNumero,
        IReadOnlyDictionary<string, string> emailesTomados,
        HashSet<string> numerosVistos,
        HashSet<string> emailesEnArchivo)
    {
        // Obligatorios: solo Nombre y Telefono (I-08 §3). Sin telefono no hay WhatsApp.
        if (string.IsNullOrWhiteSpace(fila.Nombre) || string.IsNullOrWhiteSpace(fila.Telefono))
        {
            return PlanFila.Rechazo(fila, MotivoRechazoCarga.FilaIncompleta);
        }

        if (!_normalizador.TryNormalizar(fila.Telefono!, out var numero) || numero is null)
        {
            return PlanFila.Rechazo(fila, MotivoRechazoCarga.NumeroInvalido);
        }

        if (!numerosVistos.Add(numero.Valor))
        {
            // El primero gana; los repetidos dentro del mismo archivo se rechazan.
            return PlanFila.Rechazo(fila, MotivoRechazoCarga.DuplicadoEnArchivo);
        }

        if (fila.AntiguedadIlegible)
        {
            return PlanFila.Rechazo(fila, MotivoRechazoCarga.AntiguedadInvalida);
        }

        if (fila.Idioma is not null && !Usuario.EsIdiomaSoportado(fila.Idioma))
        {
            return PlanFila.Rechazo(fila, MotivoRechazoCarga.IdiomaInvalido);
        }

        var existente = porNumero.GetValueOrDefault(numero.Valor);

        if (fila.Email is not null)
        {
            if (!EsEmailPlausible(fila.Email))
            {
                return PlanFila.Rechazo(fila, MotivoRechazoCarga.EmailInvalido);
            }

            // Duplicado contra otro activo del maestro o contra otra fila del mismo archivo. Un email
            // de un usuario inactivo no bloquea (I-08 §3.1.g).
            var dueno = emailesTomados.GetValueOrDefault(fila.Email);
            var chocaConOtroActivo = dueno is not null && dueno != existente?.Id;
            if (chocaConOtroActivo || !emailesEnArchivo.Add(fila.Email))
            {
                return PlanFila.Rechazo(fila, MotivoRechazoCarga.EmailDuplicado);
            }
        }

        if (existente is null)
        {
            // En solo_actualizar no se crea nada, ni siquiera si hay un inactivo con ese numero.
            return modo == ModoCargaMasiva.SoloActualizar
                ? PlanFila.Rechazo(fila, MotivoRechazoCarga.NoEncontrado)
                : PlanFila.Crear(fila, numero);
        }

        if (ComparadorNombres.EsMismaPersona(existente.Nombre, fila.Nombre))
        {
            // Mismo titular (o un typo en el nombre): actualiza sin preguntar.
            return PlanFila.Actualizar(fila, numero, existente);
        }

        // Nombre claramente distinto sobre un telefono ocupado: la carga no lo resuelve sola.
        return decisiones.GetValueOrDefault(fila.Fila) switch
        {
            AccionConflictoTitular.CorregirNombre => PlanFila.Actualizar(fila, numero, existente),
            AccionConflictoTitular.Reasignar when modo != ModoCargaMasiva.SoloActualizar
                => PlanFila.Reasignar(fila, numero, existente),
            _ => PlanFila.Conflicto(fila, existente),
        };
    }

    // ---------- Pasada 2: ejecutar ----------

    private async Task<IReadOnlyList<ResultadoFilaCarga>> EjecutarAsync(
        IReadOnlyList<PlanFila> planes,
        CancellationToken cancellationToken)
    {
        var altas = planes.Count(p => p.Accion is AccionPlan.Crear or AccionPlan.Reasignar);
        var siguienteCodigo = altas == 0
            ? 0
            : await _usuarios.ReservarCodigosUsuarioAsync(altas, cancellationToken);

        var resultados = new List<ResultadoFilaCarga>(planes.Count);
        foreach (var plan in planes)
        {
            cancellationToken.ThrowIfCancellationRequested();

            switch (plan.Accion)
            {
                case AccionPlan.Rechazar:
                    resultados.Add(plan.AResultadoRechazo());
                    break;

                case AccionPlan.Crear:
                    await AsegurarTagEmpresaAsync(plan.Fila.EmpresaId, cancellationToken);
                    resultados.Add(await CrearAsync(plan, siguienteCodigo++, cancellationToken));
                    break;

                case AccionPlan.Actualizar:
                    await AsegurarTagEmpresaAsync(plan.Fila.EmpresaId, cancellationToken);
                    resultados.Add(await ActualizarAsync(plan, cancellationToken));
                    break;

                case AccionPlan.Reasignar:
                    await AsegurarTagEmpresaAsync(plan.Fila.EmpresaId, cancellationToken);
                    resultados.Add(await ReasignarAsync(plan, siguienteCodigo++, cancellationToken));
                    break;
            }
        }

        return resultados;
    }

    private async Task<ResultadoFilaCarga> CrearAsync(
        PlanFila plan,
        int codigoUsuario,
        CancellationToken cancellationToken)
    {
        var ahora = _tiempo.GetUtcNow();
        var nuevo = Usuario.Crear(
            NuevoUsuarioId(),
            codigoUsuario,
            plan.Fila.Nombre!,
            plan.Numero!,
            RolUsuario.Participante,
            EstadoRegistro.Activo,
            area: null,
            plan.Fila.Empresa,
            TagsDeEmpresa(plan.Fila.EmpresaId),
            propiedadesDinamicas: null,
            ahora,
            ahora,
            usuarioWhatsapp: null,
            plan.Fila.EmpresaId,
            plan.Fila.Sede,
            plan.Fila.Cargo,
            plan.Fila.Email,
            plan.Fila.AntiguedadAnios,
            plan.Fila.Idioma);

        await _usuarios.GuardarUsuarioAsync(nuevo, cancellationToken);
        return new ResultadoFilaCarga(
            plan.Fila.Fila,
            ResultadoCarga.Creado,
            nuevo.Id,
            Motivo: null,
            nuevo.CodigoUsuario);
    }

    private async Task<ResultadoFilaCarga> ActualizarAsync(PlanFila plan, CancellationToken cancellationToken)
    {
        var actualizado = Combinar(plan.Existente!, plan.Fila, plan.Numero!);
        await _usuarios.GuardarUsuarioAsync(actualizado, cancellationToken);
        return new ResultadoFilaCarga(
            plan.Fila.Fila,
            ResultadoCarga.Actualizado,
            actualizado.Id,
            Motivo: null,
            actualizado.CodigoUsuario);
    }

    private async Task<ResultadoFilaCarga> ReasignarAsync(
        PlanFila plan,
        int codigoUsuario,
        CancellationToken cancellationToken)
    {
        var anterior = plan.Existente!;
        var ahora = _tiempo.GetUtcNow();

        // Orden obligatorio (03 §3.1): primero inactivar al titular —su claveUnicidad pasa de
        // wa|<numero> a hist|<id>— y solo entonces crear al nuevo. Al reves, la unique key lo rechaza.
        var inactivado = Clonar(anterior, EstadoRegistro.Inactivo, ahora);
        await _usuarios.GuardarUsuarioAsync(inactivado, cancellationToken);

        var nuevo = Usuario.Crear(
            NuevoUsuarioId(),
            codigoUsuario,
            plan.Fila.Nombre!,
            plan.Numero!,
            RolUsuario.Participante,
            EstadoRegistro.Activo,
            area: null,
            plan.Fila.Empresa,
            // El nuevo titular no hereda tags, rol ni historial (I-08 §4.4).
            TagsDeEmpresa(plan.Fila.EmpresaId),
            propiedadesDinamicas: null,
            ahora,
            ahora,
            usuarioWhatsapp: null,
            plan.Fila.EmpresaId,
            plan.Fila.Sede,
            plan.Fila.Cargo,
            plan.Fila.Email,
            plan.Fila.AntiguedadAnios,
            plan.Fila.Idioma);

        try
        {
            await _usuarios.GuardarUsuarioAsync(nuevo, cancellationToken);
        }
        catch (Exception)
        {
            // Compensacion: si el alta falla, el numero no puede quedarse sin titular activo.
            var revertido = await RevertirInactivacionAsync(anterior, cancellationToken);
            return new ResultadoFilaCarga(
                plan.Fila.Fila,
                ResultadoCarga.Rechazado,
                UsuarioId: null,
                revertido
                    ? MotivoRechazoCarga.ConflictoTitular
                    : MotivoRechazoCarga.ReasignacionIncompleta,
                UsuarioIdAnterior: anterior.Id,
                CodigoUsuarioAnterior: anterior.CodigoUsuario);
        }

        await AuditarReasignacionAsync(anterior, nuevo, cancellationToken);
        return new ResultadoFilaCarga(
            plan.Fila.Fila,
            ResultadoCarga.Reasignado,
            nuevo.Id,
            Motivo: null,
            nuevo.CodigoUsuario,
            anterior.Id,
            anterior.CodigoUsuario);
    }

    private async Task<bool> RevertirInactivacionAsync(Usuario anterior, CancellationToken cancellationToken)
    {
        try
        {
            await _usuarios.GuardarUsuarioAsync(anterior, cancellationToken);
            return true;
        }
        catch (Exception)
        {
            // No se pudo revertir: el numero queda sin titular activo, recuperable a mano desde la
            // ficha del portal. Nunca quedan dos activos (I-08 §6).
            return false;
        }
    }

    // ---------- Combinacion de campos (I-08 §4.2) ----------

    /// <summary>
    /// El archivo manda para los campos del perfil, pero un valor <b>vacio no borra</b> el existente.
    /// Se conservan <c>codigoUsuario</c>, <c>usuarioWhatsapp</c>, <c>rol</c>, <c>estado</c>,
    /// <c>creadoEn</c>, las tags manuales y las propiedades dinamicas, para no degradar un admin ni
    /// reactivar un inactivo.
    /// </summary>
    private Usuario Combinar(Usuario existente, FilaParticipanteCarga fila, NumeroWhatsApp numero)
        => Usuario.Crear(
            existente.Id,
            existente.CodigoUsuario,
            fila.Nombre!,
            numero,
            existente.Rol,
            existente.Estado,
            existente.Area,
            fila.Empresa ?? existente.Empresa,
            existente.Tags.Union(TagsDeEmpresa(fila.EmpresaId), StringComparer.Ordinal),
            existente.PropiedadesDinamicas,
            existente.CreadoEn,
            _tiempo.GetUtcNow(),
            existente.UsuarioWhatsapp,
            fila.EmpresaId ?? existente.EmpresaId,
            fila.Sede ?? existente.Sede,
            fila.Cargo ?? existente.Cargo,
            fila.Email ?? existente.Email,
            fila.AntiguedadAnios ?? existente.AntiguedadAnios,
            fila.Idioma ?? existente.Idioma);

    private static Usuario Clonar(Usuario usuario, EstadoRegistro estado, DateTimeOffset ahora)
        => Usuario.Crear(
            usuario.Id,
            usuario.CodigoUsuario,
            usuario.Nombre,
            usuario.WhatsappNormalizado,
            usuario.Rol,
            estado,
            usuario.Area,
            usuario.Empresa,
            usuario.Tags,
            usuario.PropiedadesDinamicas,
            usuario.CreadoEn,
            ahora,
            usuario.UsuarioWhatsapp,
            usuario.EmpresaId,
            usuario.Sede,
            usuario.Cargo,
            usuario.Email,
            usuario.AntiguedadAnios,
            usuario.Idioma);

    // ---------- Tags, campania y auditoria ----------

    /// <summary>Tag de empresa derivada del codigo corto: <c>t_emp_&lt;idEmpresa&gt;</c> (I-08 §3).</summary>
    private static IReadOnlyCollection<string> TagsDeEmpresa(string? empresaId)
    {
        var tag = TagEmpresa(empresaId);
        return tag is null ? [] : [tag];
    }

    private static string? TagEmpresa(string? empresaId)
        => string.IsNullOrWhiteSpace(empresaId)
            ? null
            : "t_emp_" + empresaId.Trim().ToLowerInvariant();

    private async Task AsegurarTagEmpresaAsync(string? empresaId, CancellationToken cancellationToken)
    {
        var tagId = TagEmpresa(empresaId);
        if (tagId is null)
        {
            return;
        }

        var existente = await _usuarios.ObtenerTagPorIdAsync(tagId, cancellationToken);
        if (existente is not null)
        {
            return;
        }

        var tag = Tag.Crear(
            tagId,
            empresaId!.Trim(),
            TipoTagEmpresa,
            descripcion: null,
            EstadoRegistro.Activo,
            _tiempo.GetUtcNow());
        await _usuarios.GuardarTagAsync(tag, cancellationToken);
    }

    private async Task<int> AsociarSiCorrespondeAsync(
        string? campaniaId,
        IReadOnlyCollection<string> usuarioIds,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(campaniaId) || usuarioIds.Count == 0)
        {
            return 0;
        }

        var asociados = await _campanias.AsociarParticipantesAsync(
            campaniaId,
            new SolicitudAsociarParticipantes(usuarioIds.ToArray(), Filtro: null),
            cancellationToken);
        return asociados.Count;
    }

    private Task AuditarAsync(
        string? campaniaId,
        string modo,
        ReporteCargaMasiva reporte,
        CancellationToken cancellationToken)
    {
        var detalle =
            $"carga_masiva:campania={campaniaId ?? "-"}:modo={modo}:total={reporte.TotalFilas}," +
            $"creado={reporte.Creados},actualizado={reporte.Actualizados}," +
            $"reasignado={reporte.Reasignados},rechazado={reporte.Rechazados}," +
            $"asociado={reporte.Asociados}";

        return RegistrarAsync("carga_masiva", usuarioId: null, detalle, cancellationToken);
    }

    /// <summary>Auditoria de la reasignacion (I-08 §4.4): ids y codigos, nunca nombre ni numero.</summary>
    private Task AuditarReasignacionAsync(Usuario anterior, Usuario nuevo, CancellationToken cancellationToken)
        => RegistrarAsync(
            "reasignacion_numero",
            anterior.Id,
            $"reasignacion:origen=carga_masiva:codigoAnterior={anterior.CodigoUsuario}," +
            $"codigoNuevo={nuevo.CodigoUsuario}",
            cancellationToken);

    private Task RegistrarAsync(
        string accion,
        string? usuarioId,
        string detalle,
        CancellationToken cancellationToken)
        => _logSeguridad.RegistrarAsync(
            LogSeguridad.Crear(
                "log_" + Guid.NewGuid().ToString("N"),
                TipoEventoSeguridad.AccionAdministrativa,
                usuarioId,
                numero: null,
                accion,
                detalle,
                _correlacion.CorrelationIdActual,
                _tiempo.GetUtcNow()),
            cancellationToken);

    // ---------- Utilidades ----------

    private static string NuevoUsuarioId() => "u_" + Guid.NewGuid().ToString("N");

    private static string NormalizarModo(string modo)
    {
        var valor = string.IsNullOrWhiteSpace(modo) ? ModoCargaMasiva.Upsert : modo.Trim().ToLowerInvariant();
        if (!ModoCargaMasiva.EsValido(valor))
        {
            throw new ErrorValidacion(
                "El modo de carga debe ser 'upsert' o 'solo_actualizar'.",
                new[] { new DetalleError("modo", "invalido") });
        }

        return valor;
    }

    private static IReadOnlyDictionary<int, string> NormalizarResoluciones(
        IReadOnlyCollection<ResolucionConflictoTitular> resoluciones)
    {
        var decisiones = new Dictionary<int, string>();
        foreach (var resolucion in resoluciones)
        {
            var accion = resolucion.Accion?.Trim().ToLowerInvariant() ?? string.Empty;
            if (!AccionConflictoTitular.EsValida(accion))
            {
                throw new ErrorValidacion(
                    "La accion de resolucion debe ser 'corregir_nombre', 'reasignar' u 'omitir'.",
                    new[] { new DetalleError("reasignaciones", "accion_invalida") });
            }

            decisiones[resolucion.Fila] = accion;
        }

        return decisiones;
    }

    /// <summary>
    /// Chequeo minimo de forma del email; la unicidad se valida aparte. No pretende ser una validacion
    /// RFC: la plantilla V1 trae valores con espacios y ese es el ruido real que hay que atajar.
    /// </summary>
    private static bool EsEmailPlausible(string email)
    {
        var arroba = email.IndexOf('@');
        if (arroba <= 0 || arroba != email.LastIndexOf('@') || arroba == email.Length - 1)
        {
            return false;
        }

        var dominio = email[(arroba + 1)..];
        return !email.Any(char.IsWhiteSpace)
            && dominio.Contains('.')
            && !dominio.StartsWith('.')
            && !dominio.EndsWith('.');
    }

    private ILectorArchivoParticipantes ResolverLector(string nombreArchivo)
    {
        var extension = Path.GetExtension(nombreArchivo);
        var lector = _lectores.FirstOrDefault(l => l.Soporta(extension));
        return lector ?? throw new ErrorValidacion(
            "El formato del archivo no es soportado (solo .xlsx y .csv).",
            new[] { new DetalleError("archivo", "formato_no_soportado") });
    }

    private enum AccionPlan
    {
        Rechazar,
        Crear,
        Actualizar,
        Reasignar,
    }

    /// <summary>Lo que se decidio para una fila en la pasada 1, antes de tocar la base.</summary>
    private sealed record PlanFila(
        FilaParticipanteCarga Fila,
        AccionPlan Accion,
        NumeroWhatsApp? Numero,
        Usuario? Existente,
        string? Motivo)
    {
        public static PlanFila Rechazo(FilaParticipanteCarga fila, string motivo)
            => new(fila, AccionPlan.Rechazar, null, null, motivo);

        public static PlanFila Crear(FilaParticipanteCarga fila, NumeroWhatsApp numero)
            => new(fila, AccionPlan.Crear, numero, null, null);

        public static PlanFila Actualizar(FilaParticipanteCarga fila, NumeroWhatsApp numero, Usuario existente)
            => new(fila, AccionPlan.Actualizar, numero, existente, null);

        public static PlanFila Reasignar(FilaParticipanteCarga fila, NumeroWhatsApp numero, Usuario existente)
            => new(fila, AccionPlan.Reasignar, numero, existente, null);

        public static PlanFila Conflicto(FilaParticipanteCarga fila, Usuario existente)
            => new(fila, AccionPlan.Rechazar, null, existente, MotivoRechazoCarga.ConflictoTitular);

        /// <summary>
        /// En un conflicto de titular el reporte lleva ademas el titular actual y el nombre propuesto,
        /// para que el portal muestre <i>actual vs. propuesto</i> y el admin decida por fila.
        /// </summary>
        public ResultadoFilaCarga AResultadoRechazo()
            => Motivo == MotivoRechazoCarga.ConflictoTitular && Existente is not null
                ? new ResultadoFilaCarga(
                    Fila.Fila,
                    ResultadoCarga.Rechazado,
                    UsuarioId: null,
                    Motivo,
                    CodigoUsuario: null,
                    Existente.Id,
                    Existente.CodigoUsuario,
                    Existente.Nombre,
                    Fila.Nombre)
                : new ResultadoFilaCarga(Fila.Fila, ResultadoCarga.Rechazado, UsuarioId: null, Motivo);
    }
}
