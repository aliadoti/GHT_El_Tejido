using ElTejido.Application.Common;
using ElTejido.Application.Seguridad;
using ElTejido.Domain.Common;
using ElTejido.Domain.Configuracion;

namespace ElTejido.Application.Configuracion;

/// <summary>
/// Casos de uso administrativos para rubricas, prompts y ConfigLLM (04 secciones 5.5-5.7, 07 secciones 3-5).
/// </summary>
public sealed class ServicioGestionConfiguracion : IServicioGestionConfiguracion
{
    private readonly IRepositorioConfiguracion _repositorio;
    private readonly ISecretWriter _secretWriter;
    private readonly TimeProvider _tiempo;

    public ServicioGestionConfiguracion(
        IRepositorioConfiguracion repositorio,
        ISecretWriter secretWriter,
        TimeProvider tiempo)
    {
        _repositorio = repositorio;
        _secretWriter = secretWriter;
        _tiempo = tiempo;
    }

    public Task<IReadOnlyCollection<Rubrica>> BuscarRubricasAsync(
        EstadoRubrica? estado,
        CancellationToken cancellationToken)
        => _repositorio.BuscarRubricasAsync(estado, cancellationToken);

    public async Task<Rubrica> ObtenerRubricaAsync(string id, CancellationToken cancellationToken)
        => await _repositorio.ObtenerUltimaRubricaAsync(RequerirId(id), cancellationToken)
            ?? throw new ErrorNoEncontrado("La rubrica no existe.");

    public Task<IReadOnlyCollection<Rubrica>> ListarVersionesRubricaAsync(
        string id,
        CancellationToken cancellationToken)
        => _repositorio.ListarVersionesRubricaAsync(RequerirId(id), cancellationToken);

    public async Task<Rubrica> CrearRubricaAsync(SolicitudGuardarRubrica solicitud, CancellationToken cancellationToken)
    {
        var existente = await _repositorio.ObtenerUltimaRubricaAsync(RequerirId(solicitud.Id), cancellationToken);
        if (existente is not null)
        {
            throw new ErrorConflicto("Ya existe una rubrica con ese id.");
        }

        var rubrica = CrearRubrica(solicitud, version: 1, creadoEn: _tiempo.GetUtcNow());
        await _repositorio.GuardarRubricaAsync(rubrica, cancellationToken);
        return rubrica;
    }

    public async Task<Rubrica> CrearVersionRubricaAsync(
        string id,
        SolicitudGuardarRubrica solicitud,
        CancellationToken cancellationToken)
    {
        var actual = await ObtenerRubricaAsync(id, cancellationToken);
        var rubrica = CrearRubrica(
            solicitud with { Id = actual.Id },
            actual.Version + 1,
            actual.CreadoEn);
        await _repositorio.GuardarRubricaAsync(rubrica, cancellationToken);
        return rubrica;
    }

    public async Task<Rubrica> CambiarEstadoRubricaAsync(
        string id,
        EstadoRubrica estado,
        CancellationToken cancellationToken)
    {
        var actual = await ObtenerRubricaAsync(id, cancellationToken);
        var rubrica = Rubrica.Crear(
            actual.Id,
            actual.Nombre,
            actual.Descripcion,
            actual.ContenidoMarkdown,
            actual.Escala,
            actual.Criterios,
            actual.Version,
            estado,
            actual.CreadoEn,
            _tiempo.GetUtcNow());
        await _repositorio.GuardarRubricaAsync(rubrica, cancellationToken);
        return rubrica;
    }

    public Task<IReadOnlyCollection<Prompt>> BuscarPromptsAsync(
        string? tipoPrompt,
        EstadoPrompt? estado,
        CancellationToken cancellationToken)
        => _repositorio.BuscarPromptsAsync(tipoPrompt, estado, cancellationToken);

    public async Task<Prompt> ObtenerPromptAsync(string id, CancellationToken cancellationToken)
        => await _repositorio.ObtenerUltimoPromptAsync(RequerirId(id), cancellationToken)
            ?? throw new ErrorNoEncontrado("El prompt no existe.");

    public Task<IReadOnlyCollection<Prompt>> ListarVersionesPromptAsync(
        string id,
        CancellationToken cancellationToken)
        => _repositorio.ListarVersionesPromptAsync(RequerirId(id), cancellationToken);

    public async Task<Prompt> CrearPromptAsync(SolicitudGuardarPrompt solicitud, CancellationToken cancellationToken)
    {
        var existente = await _repositorio.ObtenerUltimoPromptAsync(RequerirId(solicitud.Id), cancellationToken);
        if (existente is not null)
        {
            throw new ErrorConflicto("Ya existe un prompt con ese id.");
        }

        var prompt = CrearPrompt(solicitud, version: 1, creadoEn: _tiempo.GetUtcNow(), aprobadoPor: null, fechaAprobacion: null);
        await _repositorio.GuardarPromptAsync(prompt, cancellationToken);
        return prompt;
    }

    public async Task<Prompt> CrearVersionPromptAsync(
        string id,
        SolicitudGuardarPrompt solicitud,
        CancellationToken cancellationToken)
    {
        var actual = await ObtenerPromptAsync(id, cancellationToken);
        var prompt = CrearPrompt(
            solicitud with { Id = actual.Id },
            actual.Version + 1,
            actual.CreadoEn,
            aprobadoPor: null,
            fechaAprobacion: null);
        await _repositorio.GuardarPromptAsync(prompt, cancellationToken);
        return prompt;
    }

    public async Task<Prompt> AprobarPromptAsync(string id, string usuarioId, CancellationToken cancellationToken)
    {
        var actual = await ObtenerPromptAsync(id, cancellationToken);
        var ahora = _tiempo.GetUtcNow();
        var prompt = Prompt.Crear(
            actual.Id,
            actual.Nombre,
            actual.TipoPrompt,
            actual.Contenido,
            actual.Version,
            EstadoPrompt.Activo,
            RequerirId(usuarioId),
            ahora,
            actual.CreadoEn,
            ahora);
        await _repositorio.GuardarPromptAsync(prompt, cancellationToken);
        return prompt;
    }

    public async Task<Prompt> CambiarEstadoPromptAsync(
        string id,
        EstadoPrompt estado,
        CancellationToken cancellationToken)
    {
        var actual = await ObtenerPromptAsync(id, cancellationToken);
        var prompt = Prompt.Crear(
            actual.Id,
            actual.Nombre,
            actual.TipoPrompt,
            actual.Contenido,
            actual.Version,
            estado,
            actual.AprobadoPor,
            actual.FechaAprobacion,
            actual.CreadoEn,
            _tiempo.GetUtcNow());
        await _repositorio.GuardarPromptAsync(prompt, cancellationToken);
        return prompt;
    }

    public Task<IReadOnlyCollection<ConfigLlm>> BuscarConfigsLlmAsync(
        EstadoRegistro? estado,
        CancellationToken cancellationToken)
        => _repositorio.BuscarConfigsLlmAsync(estado, cancellationToken);

    public async Task<ConfigLlm> ObtenerConfigLlmAsync(string id, CancellationToken cancellationToken)
        => await _repositorio.ObtenerConfigLlmAsync(RequerirId(id), cancellationToken)
            ?? throw new ErrorNoEncontrado("La configuracion LLM no existe.");

    public async Task<ConfigLlm> CrearConfigLlmAsync(
        SolicitudGuardarConfigLlm solicitud,
        CancellationToken cancellationToken)
    {
        var id = "llm_" + Guid.NewGuid().ToString("N");
        var apiKeyRef = ResolverApiKeyRef(solicitud.ApiKeyRef, id);
        await _secretWriter.GuardarSecretoAsync(apiKeyRef, RequerirTexto(solicitud.ApiKey, "apiKey"), cancellationToken);
        var ahora = _tiempo.GetUtcNow();
        var config = ConfigLlm.Crear(
            id,
            solicitud.Nombre,
            solicitud.Proveedor,
            solicitud.Modelo,
            solicitud.Endpoint,
            apiKeyRef,
            solicitud.Parametros,
            solicitud.LimitesTokens,
            solicitud.TimeoutSegundos,
            solicitud.MaxReintentos,
            solicitud.Estado,
            ahora,
            ahora);
        await _repositorio.GuardarConfigLlmAsync(config, cancellationToken);
        return config;
    }

    public async Task<ConfigLlm> ActualizarConfigLlmAsync(
        string id,
        SolicitudActualizarConfigLlm solicitud,
        CancellationToken cancellationToken)
    {
        var actual = await ObtenerConfigLlmAsync(id, cancellationToken);
        var apiKeyRef = solicitud.ApiKeyRef is null ? actual.ApiKeyRef : ResolverApiKeyRef(solicitud.ApiKeyRef, actual.Id);
        if (!string.IsNullOrWhiteSpace(solicitud.ApiKey))
        {
            await _secretWriter.GuardarSecretoAsync(apiKeyRef, solicitud.ApiKey, cancellationToken);
        }

        var config = ConfigLlm.Crear(
            actual.Id,
            solicitud.Nombre ?? actual.Nombre,
            solicitud.Proveedor ?? actual.Proveedor,
            solicitud.Modelo ?? actual.Modelo,
            solicitud.Endpoint ?? actual.Endpoint,
            apiKeyRef,
            solicitud.Parametros ?? actual.Parametros,
            solicitud.LimitesTokens ?? actual.LimitesTokens,
            solicitud.TimeoutSegundos ?? actual.TimeoutSegundos,
            solicitud.MaxReintentos ?? actual.MaxReintentos,
            solicitud.Estado ?? actual.Estado,
            actual.CreadoEn,
            _tiempo.GetUtcNow());
        await _repositorio.GuardarConfigLlmAsync(config, cancellationToken);
        return config;
    }

    public async Task<ConfigLlm> CambiarEstadoConfigLlmAsync(
        string id,
        EstadoRegistro estado,
        CancellationToken cancellationToken)
        => await ActualizarConfigLlmAsync(
            id,
            new SolicitudActualizarConfigLlm(null, null, null, null, null, null, null, null, null, null, estado),
            cancellationToken);

    private Rubrica CrearRubrica(SolicitudGuardarRubrica solicitud, int version, DateTimeOffset creadoEn)
        => Rubrica.Crear(
            solicitud.Id,
            solicitud.Nombre,
            solicitud.Descripcion,
            solicitud.ContenidoMarkdown,
            solicitud.Escala,
            solicitud.Criterios,
            version,
            solicitud.Estado,
            creadoEn,
            _tiempo.GetUtcNow());

    private Prompt CrearPrompt(
        SolicitudGuardarPrompt solicitud,
        int version,
        DateTimeOffset creadoEn,
        string? aprobadoPor,
        DateTimeOffset? fechaAprobacion)
        => Prompt.Crear(
            solicitud.Id,
            solicitud.Nombre,
            solicitud.TipoPrompt,
            solicitud.Contenido,
            version,
            solicitud.Estado,
            aprobadoPor,
            fechaAprobacion,
            creadoEn,
            _tiempo.GetUtcNow());

    private static string ResolverApiKeyRef(string? apiKeyRef, string id)
        => string.IsNullOrWhiteSpace(apiKeyRef) ? $"llm-key-{id}" : apiKeyRef.Trim();

    private static string RequerirId(string id)
        => RequerirTexto(id, "id");

    private static string RequerirTexto(string? valor, string campo)
    {
        if (string.IsNullOrWhiteSpace(valor))
        {
            throw new ErrorValidacion($"El campo {campo} es obligatorio.", new[] { new DetalleError(campo, "obligatorio") });
        }

        return valor.Trim();
    }
}
