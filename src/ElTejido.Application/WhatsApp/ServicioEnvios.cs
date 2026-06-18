using System.Globalization;
using ElTejido.Application.Campanas;
using ElTejido.Application.Common;
using ElTejido.Application.Participantes;
using ElTejido.Application.Usuarios;
using ElTejido.Domain.Campanas;
using ElTejido.Domain.Common;
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
    private readonly OpcionesPlantillaEnvioInicial _plantillaEnvioInicial;

    public ServicioEnvios(
        IRepositorioCampanias campanias,
        IRepositorioParticipantes participantes,
        IRepositorioUsuarios usuarios,
        IColaEnvios cola,
        IAlmacenJobs jobs,
        OpcionesPlantillaEnvioInicial plantillaEnvioInicial)
    {
        _campanias = campanias;
        _participantes = participantes;
        _usuarios = usuarios;
        _cola = cola;
        _jobs = jobs;
        _plantillaEnvioInicial = plantillaEnvioInicial;
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
        var plantilla = ResolverPlantillaEnvioInicial(mensaje);

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

            var variables = ConstruirVariables(usuario, campania);
            var trabajo = new TrabajoEnvio(
                job.Id,
                id,
                participante.UsuarioId,
                participante.WhatsappNormalizado.Valor,
                mensaje.Id,
                plantilla,
                variables,
                ReemplazarVariables(mensaje.Texto, variables),
                tipo);

            await _cola.EncolarAsync(trabajo, cancellationToken);
        }

        return new ResultadoEncolarEnvio(job.Id, job.Encolados, "enProceso");
    }

    private PlantillaWhatsApp ResolverPlantillaEnvioInicial(MensajeInicial mensaje)
    {
        if (string.IsNullOrWhiteSpace(_plantillaEnvioInicial.Nombre))
        {
            throw new ErrorReglaNegocio(
                "Configura WhatsApp__PlantillaEnvioInicial__Nombre con el nombre de una plantilla aprobada por Meta antes de enviar campanias.");
        }

        var idioma = !string.IsNullOrWhiteSpace(_plantillaEnvioInicial.Idioma)
            ? _plantillaEnvioInicial.Idioma.Trim()
            : mensaje.PlantillaWhatsApp?.Idioma;
        if (string.IsNullOrWhiteSpace(idioma))
        {
            throw new ErrorReglaNegocio(
                "Configura WhatsApp__PlantillaEnvioInicial__Idioma con el codigo exacto de idioma aprobado por Meta.");
        }

        var componentes = _plantillaEnvioInicial.Componentes
            .Select(componente => componente.Trim())
            .Where(componente => componente.Length > 0)
            .ToArray();

        if (componentes.Length == 0 && mensaje.PlantillaWhatsApp is not null)
        {
            componentes = mensaje.PlantillaWhatsApp.Componentes.ToArray();
        }

        return PlantillaWhatsApp.Crear(_plantillaEnvioInicial.Nombre, idioma, componentes);
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

    private static IReadOnlyDictionary<string, string> ConstruirVariables(
        Domain.Usuarios.Usuario usuario,
        Campania campania)
    {
        var variables = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["nombre"] = usuario.Nombre,
            ["area"] = usuario.Area,
            ["empresa"] = usuario.Empresa,
            ["campaña"] = campania.Nombre,
            ["campania"] = campania.Nombre,
        };

        // Propiedades dinamicas del usuario tambien quedan disponibles como variables (REQ §15.3).
        foreach (var propiedad in usuario.PropiedadesDinamicas)
        {
            variables[propiedad.Key] = Convert.ToString(propiedad.Value, CultureInfo.InvariantCulture) ?? string.Empty;
        }

        return variables;
    }

    private static string ReemplazarVariables(string texto, IReadOnlyDictionary<string, string> variables)
    {
        var resultado = texto;
        foreach (var variable in variables)
        {
            resultado = resultado.Replace("{{" + variable.Key + "}}", variable.Value, StringComparison.Ordinal);
        }

        return resultado;
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
}
