using ElTejido.Domain.Conversaciones;
using ElTejido.Infrastructure.Conversaciones;
using FluentAssertions;
using Microsoft.Azure.Cosmos;

namespace ElTejido.UnitTests.Conversacion;

/// <summary>
/// P-26 corte 1: round-trip Cosmos del EnrutamientoAporte (03 §3.6.1) y upsert idempotente en la
/// particion routing (un reintento reutiliza el mismo documento, nunca crea dos).
/// </summary>
public sealed class RepositorioEnrutamientosAporteCosmosTests
{
    private static readonly DateTimeOffset Ahora = new(2026, 7, 29, 15, 0, 0, TimeSpan.Zero);

    [Fact]
    public void Documento_RoundTrip_ConservaEstadoSnapshotsIntentosYFechas()
    {
        var enrutamiento = EnrutamientoAporte.Crear(
            "u_8f3c",
            "wamid.abc",
            "Se me ocurrio crear...",
            EstadoEnrutamientoAporte.SeleccionPregunta,
            Ahora,
            phoneNumberIdDestino: "123456789",
            campaniasOfrecidas: [new OpcionCampaniaOfrecida("c_1", "Innovacion comercial", 1)],
            campaniaSeleccionadaId: "c_1",
            preguntasOfrecidas: [new OpcionPreguntaOfrecida("p_1", "¿Como mejorariamos la experiencia?", 1)],
            intentosSeleccion:
            [
                new IntentoSeleccion("wamid.sel1", TipoIntentoSeleccion.Campania, ResultadoIntentoSeleccion.Invalido, Ahora.AddMinutes(5)),
            ],
            actualizadoEn: Ahora.AddMinutes(5),
            esEntradaProactiva: true);

        var documento = EnrutamientoAporteCosmosDocument.FromDomain(enrutamiento);
        var reconstruido = documento.ToDomain();

        documento.Type.Should().Be("EnrutamientoAporte");
        documento.CampaniaId.Should().Be("routing:u_8f3c", "la particion interna nunca es una campania real");
        documento.Estado.Should().Be("seleccionPregunta");
        reconstruido.Id.Should().Be(enrutamiento.Id);
        reconstruido.Estado.Should().Be(EstadoEnrutamientoAporte.SeleccionPregunta);
        reconstruido.PhoneNumberIdDestino.Should().Be("123456789");
        reconstruido.TextoOriginal.Should().Be("Se me ocurrio crear...");
        reconstruido.CampaniasOfrecidas.Should().ContainSingle()
            .Which.Should().Be(new OpcionCampaniaOfrecida("c_1", "Innovacion comercial", 1));
        reconstruido.CampaniaSeleccionadaId.Should().Be("c_1");
        reconstruido.PreguntasOfrecidas.Should().ContainSingle()
            .Which.Should().Be(new OpcionPreguntaOfrecida("p_1", "¿Como mejorariamos la experiencia?", 1));
        reconstruido.IntentosSeleccion.Should().ContainSingle()
            .Which.Should().Be(new IntentoSeleccion(
                "wamid.sel1", TipoIntentoSeleccion.Campania, ResultadoIntentoSeleccion.Invalido, Ahora.AddMinutes(5)));
        reconstruido.CreadoEn.Should().Be(Ahora);
        reconstruido.ActualizadoEn.Should().Be(Ahora.AddMinutes(5));
        reconstruido.VenceEn.Should().Be(Ahora.AddHours(24));
        reconstruido.ProcesadoEn.Should().BeNull();
        reconstruido.EsEntradaProactiva.Should().BeTrue();
    }

    [Theory]
    [InlineData(EstadoEnrutamientoAporte.SeleccionCampania, "seleccionCampania")]
    [InlineData(EstadoEnrutamientoAporte.Listo, "listo")]
    [InlineData(EstadoEnrutamientoAporte.EnIdea, "enIdea")]
    [InlineData(EstadoEnrutamientoAporte.Completado, "completado")]
    [InlineData(EstadoEnrutamientoAporte.Expirado, "expirado")]
    [InlineData(EstadoEnrutamientoAporte.Cancelado, "cancelado")]
    public void Documento_MapeaTodosLosEstadosIdaYVuelta(EstadoEnrutamientoAporte estado, string esperado)
    {
        var documento = EnrutamientoAporteCosmosDocument.FromDomain(CrearEnrutamiento(estado));

        documento.Estado.Should().Be(esperado);
        documento.ToDomain().Estado.Should().Be(estado);
    }

    [Fact]
    public async Task Guardar_DosVecesElMismoMensaje_EsIdempotenteYNoDuplicaDocumentos()
    {
        var container = new FakeConversationsCosmosContainer();
        var repositorio = new RepositorioEnrutamientosAporteCosmos(container);
        var enrutamiento = CrearEnrutamiento(EstadoEnrutamientoAporte.SeleccionCampania);

        await repositorio.GuardarAsync(enrutamiento, CancellationToken.None);
        await repositorio.GuardarAsync(enrutamiento, CancellationToken.None);

        container.Enrutamientos.Should().HaveCount(1);
        container.UltimaParticion.Should().Be("routing:u_8f3c");
    }

    [Fact]
    public async Task ObtenerPorMensaje_ResuelveElIdDeterministaYDevuelveNullSiNoExiste()
    {
        var container = new FakeConversationsCosmosContainer();
        var repositorio = new RepositorioEnrutamientosAporteCosmos(container);
        await repositorio.GuardarAsync(CrearEnrutamiento(EstadoEnrutamientoAporte.Listo), CancellationToken.None);

        var existente = await repositorio.ObtenerPorMensajeAsync("u_8f3c", "wamid.abc", CancellationToken.None);
        var ausente = await repositorio.ObtenerPorMensajeAsync("u_8f3c", "wamid.otro", CancellationToken.None);

        existente.Should().NotBeNull();
        existente!.Estado.Should().Be(EstadoEnrutamientoAporte.Listo);
        ausente.Should().BeNull();
    }

    [Fact]
    public async Task ListarPorUsuario_ConsultaSoloLaParticionRoutingDelUsuario()
    {
        var container = new FakeConversationsCosmosContainer();
        var repositorio = new RepositorioEnrutamientosAporteCosmos(container);
        await repositorio.GuardarAsync(CrearEnrutamiento(EstadoEnrutamientoAporte.SeleccionCampania), CancellationToken.None);

        var resultado = await repositorio.ListarPorUsuarioAsync("u_8f3c", CancellationToken.None);

        resultado.Should().ContainSingle().Which.UsuarioId.Should().Be("u_8f3c");
        container.UltimaParticion.Should().Be("routing:u_8f3c");
    }

    private static EnrutamientoAporte CrearEnrutamiento(EstadoEnrutamientoAporte estado)
        => EnrutamientoAporte.Crear(
            "u_8f3c",
            "wamid.abc",
            "Se me ocurrio crear...",
            estado,
            Ahora);

    private sealed class FakeConversationsCosmosContainer : IConversationsCosmosContainer
    {
        public Dictionary<string, EnrutamientoAporteCosmosDocument> Enrutamientos { get; } = new(StringComparer.Ordinal);

        public string? UltimaParticion { get; private set; }

        public Task UpsertEnrutamientoAsync(
            EnrutamientoAporteCosmosDocument document,
            string partitionKey,
            CancellationToken cancellationToken)
        {
            Enrutamientos[document.Id] = document;
            UltimaParticion = partitionKey;
            return Task.CompletedTask;
        }

        public Task<EnrutamientoAporteCosmosDocument?> ReadEnrutamientoAsync(
            string id,
            string partitionKey,
            CancellationToken cancellationToken)
        {
            UltimaParticion = partitionKey;
            return Task.FromResult(Enrutamientos.GetValueOrDefault(id));
        }

        public Task<IReadOnlyCollection<T>> QueryAsync<T>(
            QueryDefinition query,
            string partitionKey,
            CancellationToken cancellationToken)
        {
            UltimaParticion = partitionKey;
            return Task.FromResult<IReadOnlyCollection<T>>(Enrutamientos.Values.Cast<T>().ToArray());
        }

        public Task UpsertConversacionAsync(ConversacionCosmosDocument document, string partitionKey, CancellationToken cancellationToken)
            => throw new NotSupportedException();

        public Task<ConversacionCosmosDocument?> ReadConversacionAsync(string id, string partitionKey, CancellationToken cancellationToken)
            => throw new NotSupportedException();

        public Task CreateMensajeAsync(MensajeCosmosDocument document, string partitionKey, CancellationToken cancellationToken)
            => throw new NotSupportedException();

        public Task DeleteAsync(string id, string partitionKey, CancellationToken cancellationToken)
            => throw new NotSupportedException();

        public Task<IReadOnlyCollection<T>> QueryCrossPartitionAsync<T>(QueryDefinition query, CancellationToken cancellationToken)
            => throw new NotSupportedException();
    }
}
