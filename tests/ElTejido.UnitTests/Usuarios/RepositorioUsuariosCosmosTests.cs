using System.Net;
using ElTejido.Application.Common;
using ElTejido.Application.Usuarios;
using ElTejido.Domain.Common;
using ElTejido.Domain.Identidad;
using ElTejido.Domain.Usuarios;
using ElTejido.Infrastructure.Usuarios;
using FluentAssertions;
using Microsoft.Azure.Cosmos;

namespace ElTejido.UnitTests.Usuarios;

public sealed class RepositorioUsuariosCosmosTests
{
    [Fact]
    public async Task GuardarUsuarioAsync_UpsertsUsuarioUsingUsuarioPartition()
    {
        var container = new FakeUsersCosmosContainer();
        var repository = new RepositorioUsuariosCosmos(container);

        await repository.GuardarUsuarioAsync(CrearUsuario(), CancellationToken.None);

        container.UsuarioUpserts.Should().ContainSingle();
        var upsert = container.UsuarioUpserts.Single();
        upsert.PartitionKey.Should().Be(UsuarioCosmosDocument.PartitionKeyValue);
        upsert.Document.Id.Should().Be("u_1");
        upsert.Document.Type.Should().Be(UsuarioCosmosDocument.DocumentType);
        upsert.Document.Pk.Should().Be("usuario");
        upsert.Document.WhatsappNormalizado.Should().Be("573001112233");
        upsert.Document.Rol.Should().Be("participante");
        upsert.Document.Estado.Should().Be("activo");
        upsert.Document.Tags.Should().BeEquivalentTo("t_area_oper", "t_emp_ght");
        upsert.Document.PropiedadesDinamicas.Should().ContainKey("cargo");
    }

    [Fact]
    public async Task GuardarUsuarioAsync_ConflictoDeClaveUnica_LanzaErrorConflicto()
    {
        var container = new FakeUsersCosmosContainer
        {
            UsuarioUpsertException = new CosmosException(
                "Unique index constraint violation.",
                HttpStatusCode.Conflict,
                subStatusCode: 0,
                activityId: "actividad",
                requestCharge: 1),
        };
        var repository = new RepositorioUsuariosCosmos(container);

        var act = () => repository.GuardarUsuarioAsync(CrearUsuario(), CancellationToken.None);

        await act.Should().ThrowAsync<ErrorConflicto>()
            .Where(e => e.Codigo == "CONFLICT");
    }

    [Fact]
    public async Task ObtenerUsuarioPorIdAsync_MapsCosmosDocumentToDomain()
    {
        var container = new FakeUsersCosmosContainer
        {
            UsuarioReadResult = UsuarioCosmosDocument.FromDomain(CrearUsuario()),
        };
        var repository = new RepositorioUsuariosCosmos(container);

        var result = await repository.ObtenerUsuarioPorIdAsync(" u_1 ", CancellationToken.None);

        container.LastUsuarioReadId.Should().Be("u_1");
        result.Should().NotBeNull();
        result!.WhatsappNormalizado.Valor.Should().Be("573001112233");
        result.Rol.Should().Be(RolUsuario.Participante);
        result.Estado.Should().Be(EstadoRegistro.Activo);
        result.PropiedadesDinamicas.Should().ContainKey("cargo");
    }

    [Fact]
    public async Task ObtenerUsuarioPorNumeroAsync_QueriesByNormalizedNumber()
    {
        var container = new FakeUsersCosmosContainer
        {
            UsuarioQueryResult = [UsuarioCosmosDocument.FromDomain(CrearUsuario())],
        };
        var repository = new RepositorioUsuariosCosmos(container);

        var result = await repository.ObtenerUsuarioPorNumeroAsync(
            NumeroWhatsApp.FromNormalized("573001112233"),
            CancellationToken.None);

        container.LastUsuarioFilter.Should().BeEquivalentTo(
            new FiltroUsuariosCosmos("573001112233", null, null, null, null, [], null));
        result.Should().NotBeNull();
        result!.Id.Should().Be("u_1");
    }

    [Fact]
    public async Task BuscarUsuariosAsync_UsesCosmosFilterAndMapsResults()
    {
        var container = new FakeUsersCosmosContainer
        {
            UsuarioQueryResult = [UsuarioCosmosDocument.FromDomain(CrearUsuario())],
        };
        var repository = new RepositorioUsuariosCosmos(container);

        var result = await repository.BuscarUsuariosAsync(
            new FiltroUsuarios(
                RolUsuario.Participante,
                EstadoRegistro.Activo,
                "Operaciones",
                "GHT",
                ["t_emp_ght"],
                " Ana "),
            CancellationToken.None);

        container.LastUsuarioFilter.Should().BeEquivalentTo(
            new FiltroUsuariosCosmos(
                null,
                "participante",
                "activo",
                "Operaciones",
                "GHT",
                ["t_emp_ght"],
                "Ana"));
        result.Should().ContainSingle().Which.Nombre.Should().Be("Ana Perez");
    }

    [Fact]
    public async Task GuardarTagAsync_UpsertsTagUsingTagPartition()
    {
        var container = new FakeUsersCosmosContainer();
        var repository = new RepositorioUsuariosCosmos(container);

        await repository.GuardarTagAsync(CrearTag(), CancellationToken.None);

        container.TagUpserts.Should().ContainSingle();
        var upsert = container.TagUpserts.Single();
        upsert.PartitionKey.Should().Be(TagCosmosDocument.PartitionKeyValue);
        upsert.Document.Id.Should().Be("t_area_oper");
        upsert.Document.Type.Should().Be(TagCosmosDocument.DocumentType);
        upsert.Document.Pk.Should().Be("tag");
        upsert.Document.TipoTag.Should().Be("area");
        upsert.Document.Estado.Should().Be("activo");
    }

    [Fact]
    public async Task ObtenerTagPorIdAsync_MapsCosmosDocumentToDomain()
    {
        var container = new FakeUsersCosmosContainer
        {
            TagReadResult = TagCosmosDocument.FromDomain(CrearTag()),
        };
        var repository = new RepositorioUsuariosCosmos(container);

        var result = await repository.ObtenerTagPorIdAsync(" t_area_oper ", CancellationToken.None);

        container.LastTagReadId.Should().Be("t_area_oper");
        result.Should().NotBeNull();
        result!.TipoTag.Should().Be("area");
        result.Estado.Should().Be(EstadoRegistro.Activo);
    }

    [Fact]
    public async Task BuscarTagsAsync_UsesCosmosFilterAndMapsResults()
    {
        var container = new FakeUsersCosmosContainer
        {
            TagQueryResult = [TagCosmosDocument.FromDomain(CrearTag())],
        };
        var repository = new RepositorioUsuariosCosmos(container);

        var result = await repository.BuscarTagsAsync(
            new FiltroTags(" area ", EstadoRegistro.Activo),
            CancellationToken.None);

        container.LastTagFilter.Should().Be(new FiltroTagsCosmos("area", "activo"));
        result.Should().ContainSingle().Which.Nombre.Should().Be("Operaciones");
    }

    private static Usuario CrearUsuario()
    {
        return Usuario.Crear(
            "u_1",
            "Ana Perez",
            NumeroWhatsApp.FromNormalized("573001112233"),
            RolUsuario.Participante,
            EstadoRegistro.Activo,
            "Operaciones",
            "GHT",
            ["t_area_oper", "t_emp_ght"],
            new Dictionary<string, object?> { ["cargo"] = "Coordinadora" },
            new DateTimeOffset(2026, 6, 10, 12, 0, 0, TimeSpan.Zero),
            new DateTimeOffset(2026, 6, 11, 9, 0, 0, TimeSpan.Zero));
    }

    private static Tag CrearTag()
    {
        return Tag.Crear(
            "t_area_oper",
            "Operaciones",
            "area",
            "Area de operaciones",
            EstadoRegistro.Activo,
            new DateTimeOffset(2026, 6, 10, 12, 0, 0, TimeSpan.Zero));
    }

    private sealed class FakeUsersCosmosContainer : IUsersCosmosContainer
    {
        public List<(UsuarioCosmosDocument Document, string PartitionKey)> UsuarioUpserts { get; } = [];

        public List<(TagCosmosDocument Document, string PartitionKey)> TagUpserts { get; } = [];

        public string? LastUsuarioReadId { get; private set; }

        public string? LastTagReadId { get; private set; }

        public FiltroUsuariosCosmos? LastUsuarioFilter { get; private set; }

        public FiltroTagsCosmos? LastTagFilter { get; private set; }

        public UsuarioCosmosDocument? UsuarioReadResult { get; init; }

        public TagCosmosDocument? TagReadResult { get; init; }

        public IReadOnlyCollection<UsuarioCosmosDocument> UsuarioQueryResult { get; init; } = [];

        public IReadOnlyCollection<TagCosmosDocument> TagQueryResult { get; init; } = [];

        public Exception? UsuarioUpsertException { get; init; }

        public Task UpsertUsuarioAsync(
            UsuarioCosmosDocument document,
            string partitionKey,
            CancellationToken cancellationToken)
        {
            if (UsuarioUpsertException is not null)
            {
                throw UsuarioUpsertException;
            }

            UsuarioUpserts.Add((document, partitionKey));
            return Task.CompletedTask;
        }

        public Task<UsuarioCosmosDocument?> ReadUsuarioByIdAsync(
            string id,
            CancellationToken cancellationToken)
        {
            LastUsuarioReadId = id;
            return Task.FromResult(UsuarioReadResult);
        }

        public Task<IReadOnlyCollection<UsuarioCosmosDocument>> QueryUsuariosAsync(
            FiltroUsuariosCosmos filtro,
            CancellationToken cancellationToken)
        {
            LastUsuarioFilter = filtro;
            return Task.FromResult(UsuarioQueryResult);
        }

        public Task UpsertTagAsync(
            TagCosmosDocument document,
            string partitionKey,
            CancellationToken cancellationToken)
        {
            TagUpserts.Add((document, partitionKey));
            return Task.CompletedTask;
        }

        public Task<TagCosmosDocument?> ReadTagByIdAsync(
            string id,
            CancellationToken cancellationToken)
        {
            LastTagReadId = id;
            return Task.FromResult(TagReadResult);
        }

        public Task<IReadOnlyCollection<TagCosmosDocument>> QueryTagsAsync(
            FiltroTagsCosmos filtro,
            CancellationToken cancellationToken)
        {
            LastTagFilter = filtro;
            return Task.FromResult(TagQueryResult);
        }
    }
}
