using ElTejido.Application.Campanas;
using ElTejido.Application.Common;
using ElTejido.Application.Configuracion;
using ElTejido.Application.Participantes;
using ElTejido.Application.Usuarios;
using ElTejido.Domain.Campanas;
using ElTejido.Domain.Common;
using ElTejido.Domain.Localizacion;
using ElTejido.Domain.Participantes;

namespace ElTejido.Application.WhatsApp;

/// <summary>
/// Implementa el envio masivo de mensajes iniciales (04 §5.4, 05 §2.5): valida que la campania
/// este <c>activa</c>, selecciona participantes, resuelve la plantilla/variables por usuario y
/// encola un <see cref="TrabajoEnvio"/> por participante. El envio real (Graph API) y la
/// persistencia de <c>EnvioMensaje</c> los hace el trabajador de cola con <see cref="ProcesadorEnvio"/>.
/// </summary>
public sealed class ServicioEnvios : IServicioEnvios
{
    private readonly IRepositorioCampanias _campanias;
    private readonly IRepositorioParticipantes _participantes;
    private readonly IRepositorioUsuarios _usuarios;
    private readonly IColaEnvios _cola;
    private readonly IAlmacenJobs _jobs;
    private readonly IResolverPlantillaCanal _resolutorPlantillaCanal;
    private readonly OpcionesCatalogoTextos _opcionesCatalogoTextos;
    private readonly IResolutorContenidoCampania _resolutorContenidoCampania;

    public ServicioEnvios(
        IRepositorioCampanias campanias,
        IRepositorioParticipantes participantes,
        IRepositorioUsuarios usuarios,
        IColaEnvios cola,
        IAlmacenJobs jobs,
        OpcionesPlantillaEnvioInicial plantillaEnvioInicial,
        OpcionesCatalogoTextos? opcionesCatalogoTextos = null,
        IResolutorContenidoCampania? resolutorContenidoCampania = null,
        IResolverPlantillaCanal? resolutorPlantillaCanal = null)
    {
        _campanias = campanias;
        _participantes = participantes;
        _usuarios = usuarios;
        _cola = cola;
        _jobs = jobs;
        _resolutorPlantillaCanal = resolutorPlantillaCanal ?? new ResolverPlantillaCanal(plantillaEnvioInicial);
        _opcionesCatalogoTextos = opcionesCatalogoTextos ?? new OpcionesCatalogoTextos();
        _resolutorContenidoCampania = resolutorContenidoCampania ?? new ResolutorContenidoCampania();
    }

    public Task<ResultadoEncolarEnvio> EncolarInicialesAsync(
        string campaniaId,
        IReadOnlyCollection<string>? usuarioIds,
        string? mensajeInicialId,
        CancellationToken cancellationToken)
    {
        var seleccion = usuarioIds is { Count: > 0 }
            ? new HashSet<string>(usuarioIds, StringComparer.Ordinal)
            : null;

        // Idempotencia por estado de participante (03 §4): no se reenvia a quien ya tiene enviado.
        return DispararAsync(
            campaniaId,
            mensajeInicialId,
            TipoEnvioMensaje.Inicial,
            participante =>
                (seleccion is null || seleccion.Contains(participante.UsuarioId))
                && participante.EstadoEnvio != EstadoEnvio.Enviado,
            cancellationToken);
    }

    public Task<ResultadoEncolarEnvio> ReenviarSinRespuestaAsync(
        string campaniaId,
        string? mensajeInicialId,
        CancellationToken cancellationToken)
        => DispararAsync(
            campaniaId,
            mensajeInicialId,
            TipoEnvioMensaje.Reenvio,
            participante => participante.EstadoRespuesta == EstadoRespuestaParticipante.SinRespuesta,
            cancellationToken);

    public Task<ResultadoEncolarEnvio> ReintentarErroresAsync(
        string campaniaId,
        string? mensajeInicialId,
        CancellationToken cancellationToken)
        => DispararAsync(
            campaniaId,
            mensajeInicialId,
            TipoEnvioMensaje.Inicial,
            participante => participante.EstadoEnvio == EstadoEnvio.Error,
            cancellationToken);

    public async Task<IReadOnlyCollection<EstadoEnvioParticipante>> ConsultarEstadoAsync(
        string campaniaId,
        CancellationToken cancellationToken)
    {
        var id = RequerirId(campaniaId);
        var participantes = await _participantes.ListarParticipantesAsync(id, cancellationToken);
        var envios = await _participantes.ListarEnviosAsync(id, cancellationToken);

        return participantes
            .Select(participante =>
            {
                var ultimoError = envios
                    .Where(envio => envio.UsuarioId == participante.UsuarioId && envio.Error is not null)
                    .OrderByDescending(envio => envio.FechaEnvio)
                    .FirstOrDefault();

                return new EstadoEnvioParticipante(
                    participante.UsuarioId,
                    participante.WhatsappNormalizado.Valor,
                    participante.EstadoEnvio.ToString().ToLowerInvariant(),
                    participante.EstadoRespuesta == EstadoRespuestaParticipante.SinRespuesta
                        ? "sinRespuesta"
                        : "respondio",
                    ultimoError?.Error);
            })
            .ToArray();
    }

    private async Task<ResultadoEncolarEnvio> DispararAsync(
        string campaniaId,
        string? mensajeInicialId,
        TipoEnvioMensaje tipo,
        Func<ParticipanteCampania, bool> filtro,
        CancellationToken cancellationToken)
    {
        var id = RequerirId(campaniaId);
        var campania = await _campanias.ObtenerCampaniaPorIdAsync(id, cancellationToken)
            ?? throw new ErrorNoEncontrado("La campania no existe.");

        if (campania.Estado != EstadoCampania.Activa)
        {
            // 04 §5.4: solo una campania activa permite envio.
            throw new ErrorConflicto("La campania debe estar activa para enviar.");
        }

        var mensaje = ResolverMensajeInicial(campania, mensajeInicialId);

        var participantes = await _participantes.ListarParticipantesAsync(id, cancellationToken);
        var objetivos = participantes
            .Where(participante => participante.Estado == EstadoRegistro.Activo && filtro(participante))
            .ToArray();

        var job = _jobs.CrearJob(id, objetivos.Length);

        foreach (var participante in objetivos)
        {
            var usuario = await _usuarios.ObtenerUsuarioPorIdAsync(participante.UsuarioId, cancellationToken);
            if (usuario is null)
            {
                // Sin usuario no se puede construir el mensaje; se cuenta como item resuelto en error.
                _jobs.RegistrarResultado(job.Id, exito: false);
                continue;
            }

            var resolucion = ResolverContenidoParaParticipante(campania, mensaje, usuario.IdiomaInterno);
            var variables = RenderizadorMensaje.ConstruirVariables(usuario, campania, resolucion.NombreCampania);
            var trabajo = new TrabajoEnvio(
                job.Id,
                id,
                participante.UsuarioId,
                participante.WhatsappNormalizado.Valor,
                mensaje.Id,
                resolucion.Plantilla,
                variables,
                RenderizadorMensaje.Reemplazar(resolucion.Texto, variables),
                tipo,
                campania.ConfigConversacional.NumeroWhatsAppSaliente,
                resolucion.Idioma,
                resolucion.PlantillaRef,
                resolucion.Error);

            await _cola.EncolarAsync(trabajo, cancellationToken);
        }

        return new ResultadoEncolarEnvio(job.Id, job.Encolados, "enProceso");
    }

    private PlantillaWhatsApp ResolverPlantillaEnvioInicial(MensajeInicial mensaje)
    {
        var resultado = _resolutorPlantillaCanal.ResolverLegacy(mensaje.PlantillaWhatsApp);
        if (resultado is ResultadoPlantillaCanal.Disponible disponible)
        {
            return disponible.Plantilla;
        }

        var problemas = ((ResultadoPlantillaCanal.NoDisponible)resultado).Problemas;
        if (problemas.Contains(ProblemasPlantillaCanal.NombreFaltante, StringComparer.Ordinal))
        {
            throw new ErrorReglaNegocio(
                "Configura WhatsApp__PlantillaEnvioInicial__Nombre con el nombre de una plantilla aprobada por Meta antes de enviar campanias.");
        }

        throw new ErrorReglaNegocio(
            "Configura WhatsApp__PlantillaEnvioInicial__Idioma con el codigo exacto de idioma aprobado por Meta.");
    }

    private ResolucionContenidoEnvio ResolverContenidoParaParticipante(
        Campania campania,
        MensajeInicial mensaje,
        IdiomaConversacion idiomaUsuario)
    {
        var resultado = _resolutorContenidoCampania.Resolver(
            new ContextoLocalizacion(campania, idiomaUsuario, _opcionesCatalogoTextos.Habilitado)
            {
                MensajeInicialId = mensaje.Id,
            });
        if (resultado is ResultadoContenidoCampania.NoDisponible noDisponible)
        {
            var error = noDisponible.CodigoPrincipal == ResolutorContenidoCampania.CodigoIdiomaNoHabilitado
                ? "IDIOMA_CAMPANIA_NO_HABILITADO: el idioma del participante no esta habilitado en la campania."
                : "LOCALIZACION_CAMPANIA_INCOMPLETA: falta contenido localizado obligatorio para el participante.";
            return ResolucionContenidoEnvio.ConError(noDisponible.Idioma.Codigo, error);
        }

        var contenido = ((ResultadoContenidoCampania.Disponible)resultado).Contenido;
        var mensajeEfectivo = contenido.MensajesIniciales[mensaje.Id];
        if (contenido.Origen == OrigenContenidoCampania.Legacy)
        {
            return new ResolucionContenidoEnvio(
                contenido.Nombre,
                mensajeEfectivo.Texto,
                ResolverPlantillaEnvioInicial(mensaje),
                null,
                contenido.Idioma.Codigo,
                null);
        }

        var resultadoPlantilla = _resolutorPlantillaCanal.Resolver(
            mensajeEfectivo.PlantillaRef,
            contenido.Idioma);
        if (resultadoPlantilla is not ResultadoPlantillaCanal.Disponible plantillaDisponible)
        {
            return ResolucionContenidoEnvio.ConError(
                contenido.Idioma.Codigo,
                "PLANTILLA_CAMPANIA_NO_CONFIGURADA: no existe una plantilla Meta aprobada para el alias e idioma del participante.");
        }

        return new ResolucionContenidoEnvio(
            contenido.Nombre,
            mensajeEfectivo.Texto,
            plantillaDisponible.Plantilla,
            mensajeEfectivo.PlantillaRef,
            contenido.Idioma.Codigo,
            null);
    }

    private static MensajeInicial ResolverMensajeInicial(Campania campania, string? mensajeInicialId)
    {
        var activos = campania.MensajesIniciales
            .Where(mensaje => mensaje.Estado == EstadoRegistro.Activo)
            .ToArray();

        if (!string.IsNullOrWhiteSpace(mensajeInicialId))
        {
            var solicitado = mensajeInicialId.Trim();
            return activos.FirstOrDefault(mensaje => mensaje.Id == solicitado)
                ?? throw new ErrorNoEncontrado("El mensaje inicial no existe o no esta activo.");
        }

        return activos.OrderBy(mensaje => mensaje.Orden).FirstOrDefault()
            ?? throw new ErrorReglaNegocio("La campania no tiene un mensaje inicial activo.");
    }

    private static string RequerirId(string id)
    {
        if (string.IsNullOrWhiteSpace(id))
        {
            throw new ErrorValidacion(
                "El id de campania es obligatorio.",
                new[] { new DetalleError("campaniaId", "obligatorio") });
        }

        return id.Trim();
    }

    private sealed record ResolucionContenidoEnvio(
        string NombreCampania,
        string Texto,
        PlantillaWhatsApp? Plantilla,
        string? PlantillaRef,
        string Idioma,
        string? Error)
    {
        public static ResolucionContenidoEnvio ConError(string idioma, string error)
            => new(string.Empty, string.Empty, null, null, idioma, error);
    }
}
