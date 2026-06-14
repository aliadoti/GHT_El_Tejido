using ElTejido.Application.Configuracion;
using ElTejido.Domain.Common;
using ElTejido.Domain.Configuracion;
using Microsoft.Azure.Cosmos;

namespace ElTejido.Infrastructure.Configuracion;

public sealed class RepositorioConfiguracionCosmos : IRepositorioConfiguracion
{
    private readonly IConfigCosmosContainer _container;

    public RepositorioConfiguracionCosmos(Container container)
        : this(new ConfigCosmosContainer(container))
    {
    }

    internal RepositorioConfiguracionCosmos(IConfigCosmosContainer container)
    {
        _container = container;
    }

    public Task GuardarRubricaAsync(Rubrica rubrica, CancellationToken cancellationToken)
    {
        var document = ConfigCosmosDocument.FromRubrica(rubrica);
        return _container.UpsertAsync(document, document.Pk, cancellationToken);
    }

    public async Task<IReadOnlyCollection<Rubrica>> BuscarRubricasAsync(
        EstadoRubrica? estado,
        CancellationToken cancellationToken)
    {
        var documents = await _container.QueryAsync(
            ConfigCosmosDocument.RubricaType,
            null,
            estado is null ? null : (estado.Value == EstadoRubrica.Activa ? "activa" : "archivada"),
            null,
            cancellationToken);
        return documents.Select(d => d.ToRubrica()).GroupBy(r => r.Id).Select(g => g.OrderByDescending(r => r.Version).First()).ToArray();
    }

    public async Task<IReadOnlyCollection<Rubrica>> ListarVersionesRubricaAsync(
        string id,
        CancellationToken cancellationToken)
    {
        var documents = await _container.QueryAsync(ConfigCosmosDocument.RubricaType, id.Trim(), null, null, cancellationToken);
        return documents.Select(d => d.ToRubrica()).OrderByDescending(r => r.Version).ToArray();
    }

    public async Task<Rubrica?> ObtenerUltimaRubricaAsync(string id, CancellationToken cancellationToken)
        => (await ListarVersionesRubricaAsync(id, cancellationToken)).FirstOrDefault();

    public Task GuardarPromptAsync(Prompt prompt, CancellationToken cancellationToken)
    {
        var document = ConfigCosmosDocument.FromPrompt(prompt);
        return _container.UpsertAsync(document, document.Pk, cancellationToken);
    }

    public async Task<IReadOnlyCollection<Prompt>> BuscarPromptsAsync(
        string? tipoPrompt,
        EstadoPrompt? estado,
        CancellationToken cancellationToken)
    {
        var documents = await _container.QueryAsync(
            ConfigCosmosDocument.PromptType,
            null,
            estado is null ? null : ConfigCosmosDocument.ToCosmosEstadoPrompt(estado.Value),
            string.IsNullOrWhiteSpace(tipoPrompt) ? null : tipoPrompt.Trim(),
            cancellationToken);
        return documents.Select(d => d.ToPrompt()).GroupBy(p => p.Id).Select(g => g.OrderByDescending(p => p.Version).First()).ToArray();
    }

    public async Task<IReadOnlyCollection<Prompt>> ListarVersionesPromptAsync(
        string id,
        CancellationToken cancellationToken)
    {
        var documents = await _container.QueryAsync(ConfigCosmosDocument.PromptType, id.Trim(), null, null, cancellationToken);
        return documents.Select(d => d.ToPrompt()).OrderByDescending(p => p.Version).ToArray();
    }

    public async Task<Prompt?> ObtenerUltimoPromptAsync(string id, CancellationToken cancellationToken)
        => (await ListarVersionesPromptAsync(id, cancellationToken)).FirstOrDefault();

    public Task GuardarConfigLlmAsync(ConfigLlm config, CancellationToken cancellationToken)
    {
        var document = ConfigCosmosDocument.FromConfigLlm(config);
        return _container.UpsertAsync(document, document.Pk, cancellationToken);
    }

    public async Task<IReadOnlyCollection<ConfigLlm>> BuscarConfigsLlmAsync(
        EstadoRegistro? estado,
        CancellationToken cancellationToken)
    {
        var documents = await _container.QueryAsync(
            ConfigCosmosDocument.ConfigLlmType,
            null,
            estado is null ? null : ConfigCosmosDocument.ToCosmosEstadoRegistro(estado.Value),
            null,
            cancellationToken);
        return documents.Select(d => d.ToConfigLlm()).ToArray();
    }

    public async Task<ConfigLlm?> ObtenerConfigLlmAsync(string id, CancellationToken cancellationToken)
    {
        var documents = await _container.QueryAsync(ConfigCosmosDocument.ConfigLlmType, null, null, null, cancellationToken);
        return documents.FirstOrDefault(d => d.Id == id.Trim())?.ToConfigLlm();
    }
}
