using ElTejido.Application.Common;
using ElTejido.Application.Configuracion;
using ElTejido.Application.Seguridad;
using ElTejido.Domain.Common;
using ElTejido.Domain.Configuracion;
using FluentAssertions;
using NSubstitute;

namespace ElTejido.UnitTests.Configuracion;

/// <summary>
/// Edicion hibrida por estado de rubricas y prompts (04 §5.5-5.6, 07 §3-4): editar en sitio (misma
/// version) solo cuando estan en borrador; en cualquier otro estado se exige crear una nueva version
/// para no corromper los snapshots de evaluaciones pasadas.
/// </summary>
public sealed class ServicioGestionConfiguracionEdicionTests
{
    private readonly RepositorioConfiguracionFake _repo = new();
    private readonly ServicioGestionConfiguracion _servicio;

    public ServicioGestionConfiguracionEdicionTests()
        => _servicio = new ServicioGestionConfiguracion(_repo, Substitute.For<ISecretProvider>(), TimeProvider.System);

    [Fact]
    public async Task ActualizarRubrica_EnBorrador_EditaEnSitioConservandoVersion()
    {
        await _servicio.CrearRubricaAsync(SolicitudRubrica("rub_1", "Original", EstadoRubrica.Borrador), CancellationToken.None);

        var editada = await _servicio.ActualizarRubricaAsync(
            "rub_1",
            SolicitudRubrica("rub_1", "Editada", EstadoRubrica.Borrador),
            CancellationToken.None);

        editada.Version.Should().Be(1);
        editada.Nombre.Should().Be("Editada");
        editada.Estado.Should().Be(EstadoRubrica.Borrador);
        _repo.Rubricas.Should().ContainSingle();
    }

    [Fact]
    public async Task ActualizarRubrica_Activa_LanzaConflicto()
    {
        await _servicio.CrearRubricaAsync(SolicitudRubrica("rub_1", "Original", EstadoRubrica.Activa), CancellationToken.None);

        var acto = () => _servicio.ActualizarRubricaAsync(
            "rub_1",
            SolicitudRubrica("rub_1", "Editada", EstadoRubrica.Activa),
            CancellationToken.None);

        await acto.Should().ThrowAsync<ErrorConflicto>();
    }

    [Fact]
    public async Task ActualizarPrompt_EnBorrador_EditaEnSitioSinAprobacion()
    {
        await _servicio.CrearPromptAsync(SolicitudPrompt("pr_1", "Original"), CancellationToken.None);

        var editado = await _servicio.ActualizarPromptAsync(
            "pr_1",
            SolicitudPrompt("pr_1", "Editado"),
            CancellationToken.None);

        editado.Version.Should().Be(1);
        editado.Nombre.Should().Be("Editado");
        editado.Estado.Should().Be(EstadoPrompt.Borrador);
        editado.AprobadoPor.Should().BeNull();
    }

    [Fact]
    public async Task ActualizarPrompt_Aprobado_LanzaConflicto()
    {
        await _servicio.CrearPromptAsync(SolicitudPrompt("pr_1", "Original"), CancellationToken.None);
        await _servicio.AprobarPromptAsync("pr_1", "u_admin", CancellationToken.None);

        var acto = () => _servicio.ActualizarPromptAsync(
            "pr_1",
            SolicitudPrompt("pr_1", "Editado"),
            CancellationToken.None);

        await acto.Should().ThrowAsync<ErrorConflicto>();
    }

    private static SolicitudGuardarRubrica SolicitudRubrica(string id, string nombre, EstadoRubrica estado)
        => new(
            id,
            nombre,
            "desc",
            "# Rubrica",
            EscalaRubrica.Crear(1, 5),
            new[] { CriterioRubrica.Crear("Impacto", 1m) },
            estado);

    private static SolicitudGuardarPrompt SolicitudPrompt(string id, string nombre)
        => new(id, nombre, "evaluar", "Eres evaluador.", EstadoPrompt.Borrador);

    private sealed class RepositorioConfiguracionFake : IRepositorioConfiguracion
    {
        public List<Rubrica> Rubricas { get; } = [];

        public List<Prompt> Prompts { get; } = [];

        public Task GuardarRubricaAsync(Rubrica rubrica, CancellationToken cancellationToken)
        {
            Rubricas.RemoveAll(r => r.Id == rubrica.Id && r.Version == rubrica.Version);
            Rubricas.Add(rubrica);
            return Task.CompletedTask;
        }

        public Task<Rubrica?> ObtenerUltimaRubricaAsync(string id, CancellationToken cancellationToken)
            => Task.FromResult(Rubricas.Where(r => r.Id == id).OrderByDescending(r => r.Version).FirstOrDefault());

        public Task<IReadOnlyCollection<Rubrica>> ListarVersionesRubricaAsync(string id, CancellationToken cancellationToken)
            => Task.FromResult<IReadOnlyCollection<Rubrica>>(Rubricas.Where(r => r.Id == id).ToArray());

        public Task<IReadOnlyCollection<Rubrica>> BuscarRubricasAsync(EstadoRubrica? estado, CancellationToken cancellationToken)
            => Task.FromResult<IReadOnlyCollection<Rubrica>>(Rubricas);

        public Task GuardarPromptAsync(Prompt prompt, CancellationToken cancellationToken)
        {
            Prompts.RemoveAll(p => p.Id == prompt.Id && p.Version == prompt.Version);
            Prompts.Add(prompt);
            return Task.CompletedTask;
        }

        public Task<Prompt?> ObtenerUltimoPromptAsync(string id, CancellationToken cancellationToken)
            => Task.FromResult(Prompts.Where(p => p.Id == id).OrderByDescending(p => p.Version).FirstOrDefault());

        public Task<IReadOnlyCollection<Prompt>> ListarVersionesPromptAsync(string id, CancellationToken cancellationToken)
            => Task.FromResult<IReadOnlyCollection<Prompt>>(Prompts.Where(p => p.Id == id).ToArray());

        public Task<IReadOnlyCollection<Prompt>> BuscarPromptsAsync(string? tipoPrompt, EstadoPrompt? estado, CancellationToken cancellationToken)
            => Task.FromResult<IReadOnlyCollection<Prompt>>(Prompts);

        public Task GuardarConfigLlmAsync(ConfigLlm config, CancellationToken cancellationToken) => throw new NotSupportedException();

        public Task<ConfigLlm?> ObtenerConfigLlmAsync(string id, CancellationToken cancellationToken) => throw new NotSupportedException();

        public Task<IReadOnlyCollection<ConfigLlm>> BuscarConfigsLlmAsync(EstadoRegistro? estado, CancellationToken cancellationToken) => throw new NotSupportedException();
    }
}
