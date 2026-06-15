using ElTejido.Application.Common;
using ElTejido.Application.Configuracion;
using ElTejido.Application.Seguridad;
using ElTejido.Domain.Common;
using ElTejido.Domain.Configuracion;
using FluentAssertions;
using NSubstitute;

namespace ElTejido.UnitTests.Configuracion;

/// <summary>
/// Verifica el modelo seguro de ConfigLLM (10 §4): la app NO escribe la API key; solo referencia un
/// secreto que YA debe existir en Key Vault. Si no existe / no es legible, devuelve un error claro
/// (no un 500 generico).
/// </summary>
public sealed class ServicioGestionConfiguracionTests
{
    [Fact]
    public async Task CrearConfigLlm_SecretoReferenciadoExiste_GuardaSoloLaReferencia()
    {
        var secretos = Substitute.For<ISecretProvider>();
        secretos.ObtenerSecretoAsync("kv-llm", Arg.Any<CancellationToken>()).Returns("sk-real-secreta");
        var repo = new RepositorioConfiguracionEnMemoria();
        var servicio = new ServicioGestionConfiguracion(repo, secretos, TimeProvider.System);

        var config = await servicio.CrearConfigLlmAsync(Solicitud("kv-llm"), CancellationToken.None);

        config.ApiKeyRef.Should().Be("kv-llm");
        repo.Guardadas.Should().ContainSingle();
        // La app nunca recibio ni almaceno el valor real de la API key.
        await secretos.Received().ObtenerSecretoAsync("kv-llm", Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task CrearConfigLlm_SecretoNoExiste_DevuelveErrorDeValidacionClaro()
    {
        var secretos = Substitute.For<ISecretProvider>();
        secretos.ObtenerSecretoAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns<string>(_ => throw new KeyNotFoundException("no esta"));
        var servicio = new ServicioGestionConfiguracion(
            new RepositorioConfiguracionEnMemoria(),
            secretos,
            TimeProvider.System);

        var acto = () => servicio.CrearConfigLlmAsync(Solicitud("kv-inexistente"), CancellationToken.None);

        var error = await acto.Should().ThrowAsync<ErrorValidacion>();
        error.Which.Codigo.Should().Be("VALIDATION_ERROR");
        error.Which.Detalles.Should().Contain(d => d.Campo == "apiKeyRef");
    }

    [Fact]
    public async Task CrearConfigLlm_SinApiKeyRef_DevuelveErrorDeValidacion()
    {
        var servicio = new ServicioGestionConfiguracion(
            new RepositorioConfiguracionEnMemoria(),
            Substitute.For<ISecretProvider>(),
            TimeProvider.System);

        var acto = () => servicio.CrearConfigLlmAsync(Solicitud(apiKeyRef: " "), CancellationToken.None);

        await acto.Should().ThrowAsync<ErrorValidacion>();
    }

    private static SolicitudGuardarConfigLlm Solicitud(string apiKeyRef)
        => new(
            "LLM de prueba",
            "openrouter.ai",
            "deepseek/deepseek-chat",
            "https://openrouter.ai/api/v1",
            apiKeyRef,
            new Dictionary<string, object?> { ["temperature"] = 0.2 },
            LimitesTokensLlm.Crear(6000, 800),
            30,
            2,
            EstadoRegistro.Activo);

    private sealed class RepositorioConfiguracionEnMemoria : IRepositorioConfiguracion
    {
        public List<ConfigLlm> Guardadas { get; } = [];

        public Task GuardarConfigLlmAsync(ConfigLlm config, CancellationToken cancellationToken)
        {
            Guardadas.Add(config);
            return Task.CompletedTask;
        }

        public Task<ConfigLlm?> ObtenerConfigLlmAsync(string id, CancellationToken cancellationToken)
            => Task.FromResult(Guardadas.FirstOrDefault(c => c.Id == id));

        public Task<IReadOnlyCollection<ConfigLlm>> BuscarConfigsLlmAsync(EstadoRegistro? estado, CancellationToken cancellationToken)
            => Task.FromResult<IReadOnlyCollection<ConfigLlm>>(Guardadas);

        public Task GuardarRubricaAsync(Rubrica rubrica, CancellationToken cancellationToken) => throw new NotSupportedException();

        public Task<Rubrica?> ObtenerUltimaRubricaAsync(string id, CancellationToken cancellationToken) => throw new NotSupportedException();

        public Task<IReadOnlyCollection<Rubrica>> ListarVersionesRubricaAsync(string id, CancellationToken cancellationToken) => throw new NotSupportedException();

        public Task<IReadOnlyCollection<Rubrica>> BuscarRubricasAsync(EstadoRubrica? estado, CancellationToken cancellationToken) => throw new NotSupportedException();

        public Task GuardarPromptAsync(Prompt prompt, CancellationToken cancellationToken) => throw new NotSupportedException();

        public Task<Prompt?> ObtenerUltimoPromptAsync(string id, CancellationToken cancellationToken) => throw new NotSupportedException();

        public Task<IReadOnlyCollection<Prompt>> ListarVersionesPromptAsync(string id, CancellationToken cancellationToken) => throw new NotSupportedException();

        public Task<IReadOnlyCollection<Prompt>> BuscarPromptsAsync(string? tipoPrompt, EstadoPrompt? estado, CancellationToken cancellationToken) => throw new NotSupportedException();
    }
}
