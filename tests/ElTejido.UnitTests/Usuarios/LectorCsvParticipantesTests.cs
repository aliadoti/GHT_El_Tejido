using System.Text;
using ElTejido.Application.Common;
using ElTejido.Application.Usuarios.CargaMasiva;
using FluentAssertions;

namespace ElTejido.UnitTests.Usuarios;

/// <summary>
/// I-08 v2 §7 — el lector CSV sobre la plantilla oficial de GHT: cabecera exacta, 9 columnas por
/// posicion, filas vacias descartadas y conversiones (antiguedad decimal) sin validar reglas de
/// negocio, que son del servicio.
/// </summary>
public sealed class LectorCsvParticipantesTests
{
    private const string Cabecera =
        "Empresa,ID Empresa,Sede,Nombre,Cargo,Email,Antigüedad en la empresa en años,Idioma,Telefono\n";

    [Fact]
    public async Task Leer_MapeaLasNueveColumnasEnOrden()
    {
        var csv = Cabecera +
            "Flores El Aljibe,AL,FF - ADM,ANA PEREZ,Coordinadora,ana@ght.com,16.391666,es,573001112233\n";

        var filas = await Leer(csv);

        var fila = filas.Should().ContainSingle().Subject;
        fila.Fila.Should().Be(2); // La cabecera es la fila 1, como en la hoja de calculo.
        fila.Empresa.Should().Be("Flores El Aljibe");
        fila.EmpresaId.Should().Be("AL");
        fila.Sede.Should().Be("FF - ADM");
        fila.Nombre.Should().Be("ANA PEREZ");
        fila.Cargo.Should().Be("Coordinadora");
        fila.Email.Should().Be("ana@ght.com");
        fila.AntiguedadAnios.Should().Be(16.391666m);
        fila.AntiguedadIlegible.Should().BeFalse();
        fila.Idioma.Should().Be("es");
        fila.Telefono.Should().Be("573001112233");
    }

    [Fact]
    public async Task Leer_CeldasVaciasLleganComoNull()
    {
        var csv = Cabecera + ",,,ANA PEREZ,,,,,573001112233\n";

        var fila = (await Leer(csv)).Should().ContainSingle().Subject;

        fila.Empresa.Should().BeNull();
        fila.EmpresaId.Should().BeNull();
        fila.Sede.Should().BeNull();
        fila.Cargo.Should().BeNull();
        fila.Email.Should().BeNull();
        fila.AntiguedadAnios.Should().BeNull();
        fila.AntiguedadIlegible.Should().BeFalse();
        fila.Idioma.Should().BeNull();
    }

    [Fact]
    public async Task Leer_DescartaFilasTotalmenteVacias()
    {
        // La V1 de GHT trae una fila en blanco al final.
        var csv = Cabecera +
            ",,,ANA PEREZ,,,,,573001112233\n" +
            ",,,,,,,,\n" +
            ",,,BETO GOMEZ,,,,,573009998877\n";

        var filas = await Leer(csv);

        filas.Should().HaveCount(2);
        filas.Select(f => f.Fila).Should().Equal(2, 4); // Conserva el numero real de fila del archivo.
    }

    [Theory]
    [InlineData("16,391666", 16.391666)]
    [InlineData("16.391666", 16.391666)]
    [InlineData("3", 3)]
    public async Task Leer_AceptaComaOPuntoComoSeparadorDecimal(string texto, double esperado)
    {
        var csv = Cabecera + $",,,ANA PEREZ,,,\"{texto}\",,573001112233\n";

        var fila = (await Leer(csv)).Should().ContainSingle().Subject;

        fila.AntiguedadAnios.Should().Be((decimal)esperado);
        fila.AntiguedadIlegible.Should().BeFalse();
    }

    [Fact]
    public async Task Leer_AntiguedadNoNumerica_SeMarcaIlegibleSinPerderLaFila()
    {
        var csv = Cabecera + ",,,ANA PEREZ,,,no-es-numero,,573001112233\n";

        var fila = (await Leer(csv)).Should().ContainSingle().Subject;

        fila.AntiguedadAnios.Should().BeNull();
        fila.AntiguedadIlegible.Should().BeTrue();
    }

    [Fact]
    public async Task Leer_ToleraTildesYMayusculasEnLaCabecera()
    {
        var csv =
            "EMPRESA,id empresa,SEDE,nombre,CARGO,email,Antiguedad en la empresa en anos,IDIOMA,telefono\n" +
            ",,,ANA PEREZ,,,,,573001112233\n";

        var filas = await Leer(csv);

        filas.Should().ContainSingle();
    }

    [Fact]
    public async Task Leer_CabeceraDeLaPlantillaAnterior_EsErrorDeValidacion()
    {
        var csv = "Nombre,WhatsApp,Area,Empresa,Tags\nAna,573001112233,Ops,GHT,\n";

        var act = () => Leer(csv);

        await act.Should().ThrowAsync<ErrorValidacion>();
    }

    [Fact]
    public async Task Leer_ColumnasFueraDeOrden_EsErrorDeValidacion()
    {
        var csv =
            "ID Empresa,Empresa,Sede,Nombre,Cargo,Email,Antigüedad en la empresa en años,Idioma,Telefono\n";

        var act = () => Leer(csv);

        await act.Should().ThrowAsync<ErrorValidacion>();
    }

    [Fact]
    public async Task Leer_ArchivoVacio_EsErrorDeValidacion()
    {
        var act = () => Leer(string.Empty);

        await act.Should().ThrowAsync<ErrorValidacion>();
    }

    [Fact]
    public async Task Leer_RespetaComillasYComasDentroDeUnCampo()
    {
        var csv = Cabecera +
            "\"Flores, El Aljibe\",AL,,ANA PEREZ,\"Jefa de Postcosecha, turno 2\",,,,573001112233\n";

        var fila = (await Leer(csv)).Should().ContainSingle().Subject;

        fila.Empresa.Should().Be("Flores, El Aljibe");
        fila.Cargo.Should().Be("Jefa de Postcosecha, turno 2");
    }

    [Fact]
    public void Soporta_SoloCsv()
    {
        var lector = new LectorCsvParticipantes();

        lector.Soporta(".csv").Should().BeTrue();
        lector.Soporta(".CSV").Should().BeTrue();
        lector.Soporta(".xlsx").Should().BeFalse();
    }

    private static async Task<IReadOnlyList<FilaParticipanteCarga>> Leer(string csv)
    {
        using var contenido = new MemoryStream(Encoding.UTF8.GetBytes(csv));
        return await new LectorCsvParticipantes().LeerAsync(contenido, CancellationToken.None);
    }
}
