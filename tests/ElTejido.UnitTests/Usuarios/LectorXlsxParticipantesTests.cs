using ClosedXML.Excel;
using ElTejido.Application.Common;
using ElTejido.Application.Usuarios.CargaMasiva;
using ElTejido.Infrastructure.Usuarios;
using FluentAssertions;

namespace ElTejido.UnitTests.Usuarios;

/// <summary>
/// I-08 v2 §7 — el lector <c>.xlsx</c>, que es el formato primario (es el que entrega GHT). Debe
/// producir exactamente las mismas filas que el CSV ante los mismos contenidos, tomar la antiguedad
/// del valor numerico de la celda (no del texto formateado, que redondea a la vista) y leer el
/// telefono como texto para no perder ceros ni recibir notacion cientifica.
/// </summary>
public sealed class LectorXlsxParticipantesTests
{
    [Fact]
    public async Task Leer_MapeaLasNueveColumnasEnOrden()
    {
        using var contenido = ConstruirLibro(hoja =>
        {
            EscribirCabecera(hoja);
            hoja.Cell(2, 1).Value = "Flores El Aljibe";
            hoja.Cell(2, 2).Value = "AL";
            hoja.Cell(2, 3).Value = "FF - ADM";
            hoja.Cell(2, 4).Value = "ANA PEREZ";
            hoja.Cell(2, 5).Value = "Coordinadora";
            hoja.Cell(2, 6).Value = "ana@ght.com";
            hoja.Cell(2, 7).Value = 16.391666;
            hoja.Cell(2, 8).Value = "es";
            hoja.Cell(2, 9).Value = "573001112233";
        });

        var fila = (await Leer(contenido)).Should().ContainSingle().Subject;

        fila.Fila.Should().Be(2);
        fila.Empresa.Should().Be("Flores El Aljibe");
        fila.EmpresaId.Should().Be("AL");
        fila.Sede.Should().Be("FF - ADM");
        fila.Nombre.Should().Be("ANA PEREZ");
        fila.Cargo.Should().Be("Coordinadora");
        fila.Email.Should().Be("ana@ght.com");
        fila.AntiguedadAnios.Should().Be(16.391666m);
        fila.Idioma.Should().Be("es");
        fila.Telefono.Should().Be("573001112233");
    }

    [Fact]
    public async Task Leer_AntiguedadConFormatoDeDosDecimales_ConservaElValorSinRedondear()
    {
        using var contenido = ConstruirLibro(hoja =>
        {
            EscribirCabecera(hoja);
            hoja.Cell(2, 4).Value = "ANA PEREZ";
            hoja.Cell(2, 7).Value = 16.391666;
            hoja.Cell(2, 7).Style.NumberFormat.Format = "0.00"; // A la vista: 16,39.
            hoja.Cell(2, 9).Value = "573001112233";
        });

        var fila = (await Leer(contenido)).Should().ContainSingle().Subject;

        fila.AntiguedadAnios.Should().Be(16.391666m);
    }

    [Fact]
    public async Task Leer_DescartaFilasTotalmenteVacias()
    {
        using var contenido = ConstruirLibro(hoja =>
        {
            EscribirCabecera(hoja);
            hoja.Cell(2, 4).Value = "ANA PEREZ";
            hoja.Cell(2, 9).Value = "573001112233";
            hoja.Cell(4, 4).Value = "BETO GOMEZ";
            hoja.Cell(4, 9).Value = "573009998877";
        });

        var filas = await Leer(contenido);

        filas.Select(f => f.Fila).Should().Equal(2, 4);
    }

    [Fact]
    public async Task Leer_CabeceraDeLaPlantillaAnterior_EsErrorDeValidacion()
    {
        using var contenido = ConstruirLibro(hoja =>
        {
            var anterior = new[] { "Nombre", "WhatsApp", "Area", "Empresa", "Tags" };
            for (var columna = 0; columna < anterior.Length; columna++)
            {
                hoja.Cell(1, columna + 1).Value = anterior[columna];
            }
        });

        var act = () => Leer(contenido);

        await act.Should().ThrowAsync<ErrorValidacion>();
    }

    [Fact]
    public async Task Leer_ArchivoQueNoEsExcel_EsErrorDeValidacionYNoUn500()
    {
        using var contenido = new MemoryStream("esto no es un xlsx"u8.ToArray());

        var act = () => Leer(contenido);

        await act.Should().ThrowAsync<ErrorValidacion>();
    }

    [Fact]
    public async Task Leer_ProduceLasMismasFilasQueElLectorCsv()
    {
        using var xlsx = ConstruirLibro(hoja =>
        {
            EscribirCabecera(hoja);
            hoja.Cell(2, 1).Value = "Flores El Aljibe";
            hoja.Cell(2, 2).Value = "AL";
            hoja.Cell(2, 4).Value = "ANA PEREZ";
            hoja.Cell(2, 6).Value = "ana@ght.com";
            hoja.Cell(2, 7).Value = 16.391666;
            hoja.Cell(2, 8).Value = "es";
            hoja.Cell(2, 9).Value = "573001112233";
        });
        var csv =
            "Empresa,ID Empresa,Sede,Nombre,Cargo,Email,Antigüedad en la empresa en años,Idioma,Telefono\n" +
            "Flores El Aljibe,AL,,ANA PEREZ,,ana@ght.com,16.391666,es,573001112233\n";

        var desdeXlsx = await Leer(xlsx);
        using var contenidoCsv = new MemoryStream(System.Text.Encoding.UTF8.GetBytes(csv));
        var desdeCsv = await new LectorCsvParticipantes().LeerAsync(contenidoCsv, CancellationToken.None);

        desdeXlsx.Should().BeEquivalentTo(desdeCsv);
    }

    [Fact]
    public void Soporta_SoloXlsx()
    {
        var lector = new LectorXlsxParticipantes();

        lector.Soporta(".xlsx").Should().BeTrue();
        lector.Soporta(".XLSX").Should().BeTrue();
        lector.Soporta(".csv").Should().BeFalse();
    }

    private static Task<IReadOnlyList<FilaParticipanteCarga>> Leer(Stream contenido)
    {
        contenido.Position = 0;
        return new LectorXlsxParticipantes().LeerAsync(contenido, CancellationToken.None);
    }

    private static void EscribirCabecera(IXLWorksheet hoja)
    {
        for (var columna = 0; columna < PlantillaParticipantes.Cabecera.Count; columna++)
        {
            hoja.Cell(1, columna + 1).Value = PlantillaParticipantes.Cabecera[columna];
        }
    }

    private static MemoryStream ConstruirLibro(Action<IXLWorksheet> construir)
    {
        var contenido = new MemoryStream();
        using (var libro = new XLWorkbook())
        {
            construir(libro.Worksheets.Add("Participantes"));
            libro.SaveAs(contenido);
        }

        contenido.Position = 0;
        return contenido;
    }
}
