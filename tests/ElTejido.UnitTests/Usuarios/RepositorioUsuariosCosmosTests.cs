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

        // El filtro por estado activo lo pone el repositorio, no el llamador (I-08 §3.1.f).
        container.LastUsuarioFilter.Should().BeEquivalentTo(
            new FiltroUsuariosCosmos("573001112233", null, "activo", null, null, [], null));
        result.Should().NotBeNull();
        result!.Id.Should().Be("u_1");
    }

    [Fact]
    public async Task ListarUsuariosPorNumeroAsync_DevuelveActivoEHistoricoOrdenadoPorCreacion()
    {
        var activo = CrearUsuario();
        var anterior = Usuario.Crear(
            "u_0",
            1,
            "Titular anterior",
            NumeroWhatsApp.FromNormalized("573001112233"),
            RolUsuario.Participante,
            EstadoRegistro.Inactivo,
            "Operaciones",
            "GHT",
            [],
            null,
            new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero),
            new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero));
        var container = new FakeUsersCosmosContainer
        {
            UsuarioQueryResult =
            [
                UsuarioCosmosDocument.FromDomain(activo),
                UsuarioCosmosDocument.FromDomain(anterior),
            ],
        };
        var repository = new RepositorioUsuariosCosmos(container);

        var result = await repository.ListarUsuariosPorNumeroAsync(
            NumeroWhatsApp.FromNormalized("573001112233"),
            CancellationToken.None);

        // Sin filtro de estado: el historico es justamente lo que se quiere ver.
        container.LastUsuarioFilter!.Estado.Should().BeNull();
        result.Select(u => u.Id).Should().Equal("u_0", "u_1");
    }

    [Fact]
    public void FromDomain_CalculaClaveUnicidadSegunEstado()
    {
        var activo = CrearUsuario();
        var inactivo = Usuario.Crear(
            "u_9",
            9,
            "Titular anterior",
            NumeroWhatsApp.FromNormalized("573001112233"),
            RolUsuario.Participante,
            EstadoRegistro.Inactivo,
            null,
            null,
            [],
            null,
            DateTimeOffset.UnixEpoch,
            DateTimeOffset.UnixEpoch);

        UsuarioCosmosDocument.FromDomain(activo).ClaveUnicidad.Should().Be("wa|573001112233");
        UsuarioCosmosDocument.FromDomain(inactivo).ClaveUnicidad.Should().Be("hist|u_9");
        TagCosmosDocument.FromDomain(CrearTag()).ClaveUnicidad.Should().Be("tag|t_area_oper");
    }

    [Fact]
    public async Task ReservarCodigosUsuarioAsync_CreaElContadorYDevuelveElPrimeroDelBloque()
    {
        var container = new FakeUsersCosmosContainer();
        var repository = new RepositorioUsuariosCosmos(container);

        var primero = await repository.ReservarCodigosUsuarioAsync(5, CancellationToken.None);

        primero.Should().Be(1);
        var guardada = container.SecuenciaGuardadas.Should().ContainSingle().Subject;
        guardada.Etag.Should().BeNull();
        guardada.Document.Id.Should().Be("seq_usuario");
        guardada.Document.Pk.Should().Be("secuencia");
        guardada.Document.ClaveUnicidad.Should().Be("seq|seq_usuario");
        guardada.Document.UltimoValor.Should().Be(5);
    }

    [Fact]
    public async Task ReservarCodigosUsuarioAsync_ContinuaDesdeElUltimoValorUsandoIfMatch()
    {
        var container = new FakeUsersCosmosContainer
        {
            SecuenciaActual = CrearSecuencia(130, "etag-1"),
        };
        var repository = new RepositorioUsuariosCosmos(container);

        var primero = await repository.ReservarCodigosUsuarioAsync(2, CancellationToken.None);

        primero.Should().Be(131);
        var guardada = container.SecuenciaGuardadas.Should().ContainSingle().Subject;
        guardada.Etag.Should().Be("etag-1");
        guardada.Document.UltimoValor.Should().Be(132);
    }

    [Fact]
    public async Task ReservarCodigosUsuarioAsync_ReintentaCuandoOtroLoteGanaLaCarrera()
    {
        var container = new FakeUsersCosmosContainer
        {
            SecuenciaActual = CrearSecuencia(10, "etag-1"),
            FallosDeConcurrencia = 1,
        };
        var repository = new RepositorioUsuariosCosmos(container);

        var primero = await repository.ReservarCodigosUsuarioAsync(1, CancellationToken.None);

        primero.Should().Be(11);
        container.LecturasSecuencia.Should().Be(2);
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
            1,
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

    private static SecuenciaCosmosDocument CrearSecuencia(int ultimoValor, string etag)
        => new()
        {
            Id = SecuenciaCosmosDocument.IdUsuario,
            ClaveUnicidad = SecuenciaCosmosDocument.ConstruirClaveUnicidad(SecuenciaCosmosDocument.IdUsuario),
            UltimoValor = ultimoValor,
            ActualizadoEn = DateTimeOffset.UnixEpoch,
            ETag = etag,
        };

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

        public List<string> UsuarioDeletes { get; } = [];

        public Task DeleteUsuarioAsync(string id, CancellationToken cancellationToken)
        {
            UsuarioDeletes.Add(id);
            return Task.CompletedTask;
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

        public SecuenciaCosmosDocument? SecuenciaActual { get; set; }

        /// <summary>Cuantas veces la escritura del contador debe fallar por concurrencia antes de pasar.</summary>
        public int FallosDeConcurrencia { get; set; }

        public int LecturasSecuencia { get; private set; }

        public List<(SecuenciaCosmosDocument Document, string? Etag)> SecuenciaGuardadas { get; } = [];

        public Task<SecuenciaCosmosDocument?> ReadSecuenciaAsync(string id, CancellationToken cancellationToken)
        {
            LecturasSecuencia++;
            return Task.FromResult(SecuenciaActual);
        }

        public Task GuardarSecuenciaAsync(
            SecuenciaCosmosDocument document,
            string? etag,
            CancellationToken cancellationToken)
        {
            if (FallosDeConcurrencia > 0)
            {
                FallosDeConcurrencia--;
                throw new CosmosException(
                    "PreconditionFailed",
                    HttpStatusCode.PreconditionFailed,
                    subStatusCode: 0,
                    activityId: "actividad",
                    requestCharge: 1);
            }

            SecuenciaGuardadas.Add((document, etag));
            return Task.CompletedTask;
        }
    }
}
