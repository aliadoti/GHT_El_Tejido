using ElTejido.Domain.Identidad;
using ElTejido.Domain.Seguridad;
using ElTejido.Infrastructure.Seguridad;
using FluentAssertions;

namespace ElTejido.UnitTests.Seguridad;

public sealed class RepositoriosSeguridadCosmosTests
{
    [Fact]
    public async Task GuardarAsync_UpsertsCodigoUsingSecurityPartition()
    {
        var container = new FakeSecurityCosmosContainer();
        var repository = new RepositorioCodigosAuthCosmos(container);

        await repository.GuardarAsync(CrearCodigo(), CancellationToken.None);

        container.CodigoUpserts.Should().ContainSingle();
        var upsert = container.CodigoUpserts.Single();
        upsert.PartitionKey.Should().Be(CodigoAuthAdminCosmosDocument.PartitionKeyValue);
        upsert.Document.Pk.Should().Be("CodigoAuthAdmin");
        upsert.Document.Ttl.Should().Be(600);
        upsert.Document.HashCodigo.Should().Be("$hash$");
    }

    [Fact]
    public async Task ObtenerVigenteMasRecienteAsync_QueriesByNumberAndMaps()
    {
        var container = new FakeSecurityCosmosContainer
        {
            CodigoQueryResult = CodigoAuthAdminCosmosDocument.FromDomain(CrearCodigo()),
        };
        var repository = new RepositorioCodigosAuthCosmos(container);

        var result = await repository.ObtenerVigenteMasRecienteAsync(
            NumeroWhatsApp.FromNormalized("573001119999"),
            CancellationToken.None);

        container.LastCodigoNumero.Should().Be("573001119999");
        result.Should().NotBeNull();
        result!.UsuarioId.Should().Be("u_admin1");
    }

    [Fact]
    public async Task RegistrarAsync_CreatesLogAppendOnly()
    {
        var container = new FakeSecurityCosmosContainer();
        var repository = new RepositorioLogSeguridadCosmos(container);

        var log = LogSeguridad.Crear(
            "log_1",
            TipoEventoSeguridad.LoginFallido,
            null,
            "573001119999",
            "rechazado",
            "codigo invalido",
            "corr_1",
            new DateTimeOffset(2026, 6, 12, 15, 6, 0, TimeSpan.Zero));

        await repository.RegistrarAsync(log, CancellationToken.None);

        container.LogCreates.Should().ContainSingle();
        var create = container.LogCreates.Single();
        create.PartitionKey.Should().Be(LogSeguridadCosmosDocument.PartitionKeyValue);
        create.Document.TipoEvento.Should().Be("loginFallido");
        create.Document.Resultado.Should().Be("rechazado");
    }

    private static CodigoAuthAdmin CrearCodigo()
    {
        var creado = new DateTimeOffset(2026, 6, 12, 15, 4, 0, TimeSpan.Zero);
        return CodigoAuthAdmin.Crear(
            "otp_1",
            "u_admin1",
            NumeroWhatsApp.FromNormalized("573001119999"),
            "$hash$",
            creado.AddMinutes(5),
            5,
            false,
            creado,
            600);
    }

    private sealed class FakeSecurityCosmosContainer : ISecurityCosmosContainer
    {
        public List<(CodigoAuthAdminCosmosDocument Document, string PartitionKey)> CodigoUpserts { get; } = [];

        public List<(LogSeguridadCosmosDocument Document, string PartitionKey)> LogCreates { get; } = [];

        public string? LastCodigoNumero { get; private set; }

        public CodigoAuthAdminCosmosDocument? CodigoQueryResult { get; init; }

        public Task UpsertCodigoAsync(
            CodigoAuthAdminCosmosDocument document,
            string partitionKey,
            CancellationToken cancellationToken)
        {
            CodigoUpserts.Add((document, partitionKey));
            return Task.CompletedTask;
        }

        public Task<CodigoAuthAdminCosmosDocument?> QueryCodigoMasRecienteAsync(
            string numero,
            CancellationToken cancellationToken)
        {
            LastCodigoNumero = numero;
            return Task.FromResult(CodigoQueryResult);
        }

        public Task CreateLogAsync(
            LogSeguridadCosmosDocument document,
            string partitionKey,
            CancellationToken cancellationToken)
        {
            LogCreates.Add((document, partitionKey));
            return Task.CompletedTask;
        }
    }
}
