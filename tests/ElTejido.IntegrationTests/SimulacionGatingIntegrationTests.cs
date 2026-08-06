using System.Net;
using System.Net.Http.Json;
using System.Collections.Concurrent;
using ElTejido.Application.Common;
using ElTejido.Application.Seguridad;
using ElTejido.Application.Usuarios;
using ElTejido.Application.WhatsApp;
using ElTejido.Domain.Identidad;
using ElTejido.Domain.Seguridad;
using ElTejido.Domain.Usuarios;
using FluentAssertions;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using NSubstitute;

namespace ElTejido.IntegrationTests;

/// <summary>
/// Verifica el gating de los endpoints de simulacion (<c>/diagnostico/simulacion/*</c>) fuera de
/// Development (guia de prueba simulada §7): no se mapean sin <c>Simulacion:Habilitada</c>, y cuando
/// se habilitan exigen la clave de diagnostico (X-Diag-Key). En Development siguen disponibles sin
/// clave (cubierto por el flujo local). Estos endpoints crean admin/emiten OTP, por eso van cerrados.
/// </summary>
public sealed class SimulacionGatingIntegrationTests
{
    private const string Clave = "clave-de-diagnostico-de-pruebas";
    private const string NumeroAdmin = "573001119999";

    [Fact]
    public async Task Produccion_SinHabilitar_NoMapeaSimulacion()
    {
        using var fabrica = Construir(habilitada: false, conClave: true);
        using var client = fabrica.CreateClient();
        client.DefaultRequestHeaders.Add("X-Diag-Key", Clave);

        using var respuesta = await client.PostAsJsonAsync(
            "/diagnostico/simulacion/admin-inicial",
            new { numero = NumeroAdmin, nombre = "Admin" });

        respuesta.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Produccion_HabilitadaSinClaveCorrecta_Responde404()
    {
        using var fabrica = Construir(habilitada: true, conClave: true);
        using var client = fabrica.CreateClient();
        client.DefaultRequestHeaders.Add("X-Diag-Key", "clave-equivocada");

        using var respuesta = await client.PostAsJsonAsync(
            "/diagnostico/simulacion/admin-inicial",
            new { numero = NumeroAdmin, nombre = "Admin" });

        // P-17 (API-001): el filtro de clave de diagnostico responde 404 con cuerpo de error uniforme
        // (04 §3) + correlationId, indistinguible de no-mapeado y sin revelar la postura de la clave.
        respuesta.StatusCode.Should().Be(HttpStatusCode.NotFound);
        var correlationHeader = respuesta.Headers.GetValues("X-Correlation-Id").Should().ContainSingle().Subject;
        var cuerpo = await respuesta.Content.ReadFromJsonAsync<CuerpoErrorTest>();
        cuerpo!.Error.Code.Should().Be("NOT_FOUND");
        cuerpo.Error.CorrelationId.Should().StartWith("corr_").And.Be(correlationHeader);
    }

    [Fact]
    public async Task Produccion_HabilitadaConClaveCorrecta_CreaAdmin()
    {
        using var fabrica = Construir(habilitada: true, conClave: true);
        using var client = fabrica.CreateClient();
        client.DefaultRequestHeaders.Add("X-Diag-Key", Clave);

        using var respuesta = await client.PostAsJsonAsync(
            "/diagnostico/simulacion/admin-inicial",
            new { numero = NumeroAdmin, nombre = "Admin" });

        respuesta.StatusCode.Should().Be(HttpStatusCode.OK);
        var cuerpo = await respuesta.Content.ReadFromJsonAsync<AdminDto>();
        cuerpo!.WhatsappNormalizado.Should().Be(NumeroAdmin);
        cuerpo.Rol.Should().Be("admin");
    }

    [Fact]
    public async Task Produccion_HabilitadaConClaveCorrecta_EncolaWebhookNormalizadoYLoAuditaSinTextoNiNumero()
    {
        var cola = new ColaCaptura();
        var logs = Substitute.For<IRepositorioLogSeguridad>();
        var correlacion = Substitute.For<IProveedorCorrelacion>();
        correlacion.CorrelationIdActual.Returns("corr_dtqa01");
        using var fabrica = Construir(habilitada: true, conClave: true, cola, logs, correlacion);
        using var client = fabrica.CreateClient();
        client.DefaultRequestHeaders.Add("X-Diag-Key", Clave);

        using var respuesta = await client.PostAsJsonAsync(
            "/diagnostico/simulacion/webhook-entrante",
            new
            {
                numero = "+57 300 111 2201",
                texto = "Mensaje de prueba que no debe llegar al log.",
                whatsappMessageId = "wamid.DTQA.1",
                phoneNumberIdDestino = "phone-destino-1",
            });

        respuesta.StatusCode.Should().Be(HttpStatusCode.OK);
        var payload = cola.Payloads.Should().ContainSingle().Subject;
        var value = payload.Entry!.Single().Changes!.Single().Value!;
        var mensaje = value.Messages!.Single();
        mensaje.From.Should().Be("573001112201");
        mensaje.Id.Should().Be("wamid.DTQA.1");
        mensaje.Type.Should().Be("text");
        mensaje.Text!.Body.Should().Be("Mensaje de prueba que no debe llegar al log.");
        value.Metadata!.PhoneNumberId.Should().Be("phone-destino-1");

        await logs.Received(1).RegistrarAsync(
            Arg.Is<LogSeguridad>(log => log.TipoEvento == TipoEventoSeguridad.SimulacionWebhookEntrante
                && log.Resultado == "encolado"
                && log.CorrelationId == "corr_dtqa01"
                && log.Numero == null
                && !log.Detalle!.Contains("Mensaje de prueba", StringComparison.Ordinal)
                && !log.Detalle.Contains("573001112201", StringComparison.Ordinal)),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Produccion_Habilitada_SinMessageIdDerivaElMismoIdParaReintentosDelDedupe()
    {
        var cola = new ColaCaptura();
        using var fabrica = Construir(habilitada: true, conClave: true, cola);
        using var client = fabrica.CreateClient();
        client.DefaultRequestHeaders.Add("X-Diag-Key", Clave);

        var mensaje = new { numero = NumeroAdmin, texto = "Misma entrada de prueba" };
        using var primera = await client.PostAsJsonAsync("/diagnostico/simulacion/webhook-entrante", mensaje);
        using var segunda = await client.PostAsJsonAsync("/diagnostico/simulacion/webhook-entrante", mensaje);

        primera.StatusCode.Should().Be(HttpStatusCode.OK);
        segunda.StatusCode.Should().Be(HttpStatusCode.OK);
        var ids = cola.Payloads
            .Select(payload => payload.Entry!.Single().Changes!.Single().Value!.Messages!.Single().Id)
            .ToArray();
        ids.Should().HaveCount(2);
        ids.Distinct().Should().ContainSingle();
        ids[0].Should().Be(ids[1]).And.StartWith("sim_");
    }

    [Fact]
    public async Task Produccion_Habilitada_ElWebhookRealSigueRechazandoMensajesSinFirma()
    {
        using var fabrica = Construir(habilitada: true, conClave: true);
        using var client = fabrica.CreateClient();

        using var respuesta = await client.PostAsJsonAsync(
            "/webhook/whatsapp",
            new { entry = Array.Empty<object>() });

        respuesta.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    private static WebApplicationFactory<Program> Construir(
        bool habilitada,
        bool conClave,
        IColaWebhook? cola = null,
        IRepositorioLogSeguridad? logs = null,
        IProveedorCorrelacion? correlacion = null)
        => new WebApplicationFactory<Program>().WithWebHostBuilder(builder =>
        {
            builder.UseEnvironment("Production");
            builder.ConfigureAppConfiguration((_, config) =>
            {
                var valores = new Dictionary<string, string?>
                {
                    ["Simulacion:Habilitada"] = habilitada ? "true" : "false",
                };

                if (conClave)
                {
                    valores["Diagnostico:Clave"] = Clave;
                }

                config.AddInMemoryCollection(valores);
            });
            builder.ConfigureTestServices(services =>
            {
                // La simulacion resuelve los repos desde RequestServices; los proveemos en memoria
                // sin depender del modo de persistencia (evita el timing de config del factory).
                services.AddSingleton<IRepositorioUsuarios, UsuariosEnMemoria>();
                if (cola is not null)
                {
                    services.AddSingleton(cola);
                }
                if (logs is not null)
                {
                    services.AddSingleton(logs);
                }
                if (correlacion is not null)
                {
                    services.AddSingleton(correlacion);
                }
            });
        });

    private sealed record AdminDto(string Id, string Nombre, string WhatsappNormalizado, string Rol, string Estado);

    private sealed class ColaCaptura : IColaWebhook
    {
        public ConcurrentQueue<WhatsAppWebhookPayload> Payloads { get; } = new();

        public ValueTask EncolarAsync(WhatsAppWebhookPayload payload, CancellationToken cancellationToken)
        {
            Payloads.Enqueue(payload);
            return ValueTask.CompletedTask;
        }
    }

    private sealed class UsuariosEnMemoria : IRepositorioUsuarios
    {
        private readonly Dictionary<string, Usuario> _porNumero = new();

        public Task<Usuario?> ObtenerUsuarioPorNumeroAsync(NumeroWhatsApp numero, CancellationToken cancellationToken)
            => Task.FromResult(_porNumero.GetValueOrDefault(numero.Valor));

        public Task GuardarUsuarioAsync(Usuario usuario, CancellationToken cancellationToken)
        {
            _porNumero[usuario.WhatsappNormalizado.Valor] = usuario;
            return Task.CompletedTask;
        }

        public Task<Usuario?> ObtenerUsuarioPorIdAsync(string id, CancellationToken cancellationToken)
            => throw new NotSupportedException();

        public Task<IReadOnlyCollection<Usuario>> BuscarUsuariosAsync(FiltroUsuarios filtro, CancellationToken cancellationToken)
            => throw new NotSupportedException();

        public Task GuardarTagAsync(Tag tag, CancellationToken cancellationToken)
            => throw new NotSupportedException();

        public Task<Tag?> ObtenerTagPorIdAsync(string id, CancellationToken cancellationToken)
            => throw new NotSupportedException();

        public Task<IReadOnlyCollection<Tag>> BuscarTagsAsync(FiltroTags filtro, CancellationToken cancellationToken)
            => throw new NotSupportedException();

        public Task<int> EliminarUsuariosNoAdministrativosAsync(CancellationToken cancellationToken)
            => throw new NotSupportedException();
    }
}
