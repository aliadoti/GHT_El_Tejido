using System.Text;
using ElTejido.Application.Common;
using ElTejido.Application.Usuarios.CargaMasiva;
using FluentAssertions;

namespace ElTejido.UnitTests.Usuarios;

/// <summary>
/// I-08 — parser CSV de la plantilla de carga masiva: columnas fijas, tags por <c>;</c>, comillas
/// RFC 4180, saltos de linea, numeracion de fila 1-based y validacion de cabecera.
/// </summary>
public sealed class LectorCsvParticipantesTests
{
    private static readonly LectorCsvParticipantes Lector = new();

    [Fact]
    public void Soporta_SoloCsv()
    {
        Lector.Soporta(".csv").Should().BeTrue();
        Lector.Soporta(".CSV").Should().BeTrue();
        Lector.Soporta(".xlsx").Should().BeFalse();
    }

    [Fact]
    public async Task Leer_FilasValidas_SeparaColumnasYTagsYNumeraDesdeLaFila2()
    {
        var csv =
            "Nombre,WhatsApp,Area,Empresa,Tags\n" +
            "Ana Perez,573001112233,Operaciones,GHT,t_area_oper;t_lider\n" +
            "Beto,573009998877,Ventas,GHT,\n";

        var filas = await Lector.LeerAsync(Contenido(csv), CancellationToken.None);

        filas.Should().HaveCount(2);
        filas[0].Fila.Should().Be(2);
        filas[0].Nombre.Should().Be("Ana Perez");
        filas[0].Numero.Should().Be("573001112233");
        filas[0].Area.Should().Be("Operaciones");
        filas[0].Empresa.Should().Be("GHT");
        filas[0].Tags.Should().Equal("t_area_oper", "t_lider");
        filas[1].Fila.Should().Be(3);
        filas[1].Tags.Should().BeEmpty();
    }

    [Fact]
    public async Task Leer_CamposEntrecomillados_RespetanComasSaltosYComillasEscapadas()
    {
        var csv =
            "Nombre,WhatsApp,Area,Empresa,Tags\n" +
            "\"Perez, Ana\",573001112233,\"Ops\nNorte\",\"Dijo \"\"hola\"\"\",t1\n";

        var filas = await Lector.LeerAsync(Contenido(csv), CancellationToken.None);

        filas.Should().ContainSingle();
        filas[0].Nombre.Should().Be("Perez, Ana");
        filas[0].Area.Should().Be("Ops\nNorte");
        filas[0].Empresa.Should().Be("Dijo \"hola\"");
    }

    [Fact]
    public async Task Leer_LineasEnBlanco_SeIgnoranPeroConservanElNumeroDeFila()
    {
        var csv =
            "Nombre,WhatsApp,Area,Empresa,Tags\r\n" +
            "Ana,573001112233,Ops,GHT,t1\r\n" +
            "\r\n" +
            "Beto,573009998877,Ventas,GHT,t2\r\n";

        var filas = await Lector.LeerAsync(Contenido(csv), CancellationToken.None);

        filas.Should().HaveCount(2);
        filas[0].Fila.Should().Be(2);
        filas[1].Fila.Should().Be(4); // La fila 3 en blanco se salta pero no se reutiliza su numero.
    }

    [Fact]
    public async Task Leer_CabeceraInvalida_LanzaErrorValidacion()
    {
        var csv = "Foo,Bar\nAna,573001112233\n";

        var accion = () => Lector.LeerAsync(Contenido(csv), CancellationToken.None);

        await accion.Should().ThrowAsync<ErrorValidacion>();
    }

    [Fact]
    public async Task Leer_ArchivoVacio_LanzaErrorValidacion()
    {
        var accion = () => Lector.LeerAsync(Contenido(string.Empty), CancellationToken.None);

        await accion.Should().ThrowAsync<ErrorValidacion>();
    }

    private static MemoryStream Contenido(string texto)
        => new(Encoding.UTF8.GetBytes(texto));
}
