using ClosedXML.Excel;
using ElTejido.Application.Common;
using ElTejido.Application.Usuarios.CargaMasiva;

namespace ElTejido.Infrastructure.Usuarios;

/// <summary>
/// Lector <c>.xlsx</c> de la plantilla oficial de GHT (I-08 §3, §4.2). Es el formato <b>primario</b>:
/// es el que entrega GHT. Lee la primera hoja, exige la cabecera de
/// <see cref="PlantillaParticipantes"/> en la fila 1 y descarta las filas totalmente vacias (la V1
/// trae una).
/// <para>
/// La antiguedad se toma del <b>valor</b> de la celda cuando Excel la guarda como numero, no de su
/// texto formateado: el formato de celda redondea a la vista (<c>16,39</c>) y la spec exige guardarla
/// sin redondear (<c>16.391666</c>). El telefono se lee siempre como texto, para no perder ceros a la
/// izquierda ni recibir notacion cientifica.
/// </para>
/// </summary>
public sealed class LectorXlsxParticipantes : ILectorArchivoParticipantes
{
    public bool Soporta(string extensionArchivo)
        => string.Equals(extensionArchivo, ".xlsx", StringComparison.OrdinalIgnoreCase);

    public Task<IReadOnlyList<FilaParticipanteCarga>> LeerAsync(
        Stream contenido,
        CancellationToken cancellationToken)
    {
        using var libro = AbrirLibro(contenido);
        var hoja = libro.Worksheets.FirstOrDefault()
            ?? throw new ErrorValidacion(
                "El archivo no tiene ninguna hoja.",
                new[] { new DetalleError("archivo", "vacio") });

        var usadas = hoja.RangeUsed();
        if (usadas is null)
        {
            throw new ErrorValidacion(
                "El archivo esta vacio.",
                new[] { new DetalleError("archivo", "vacio") });
        }

        PlantillaParticipantes.ValidarCabecera(LeerCabecera(hoja));

        var filas = new List<FilaParticipanteCarga>();
        var ultimaFila = usadas.LastRow().RowNumber();
        for (var numeroFila = 2; numeroFila <= ultimaFila; numeroFila++)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var fila = hoja.Row(numeroFila);
            if (EstaVacia(fila))
            {
                continue;
            }

            LeerAntiguedad(fila, out var antiguedad, out var antiguedadIlegible);

            filas.Add(new FilaParticipanteCarga(
                numeroFila,
                Texto(fila, PlantillaParticipantes.IndiceEmpresa),
                Texto(fila, PlantillaParticipantes.IndiceEmpresaId),
                Texto(fila, PlantillaParticipantes.IndiceSede),
                Texto(fila, PlantillaParticipantes.IndiceNombre),
                Texto(fila, PlantillaParticipantes.IndiceCargo),
                Texto(fila, PlantillaParticipantes.IndiceEmail),
                antiguedad,
                antiguedadIlegible,
                Texto(fila, PlantillaParticipantes.IndiceIdioma),
                Texto(fila, PlantillaParticipantes.IndiceTelefono)));
        }

        return Task.FromResult<IReadOnlyList<FilaParticipanteCarga>>(filas);
    }

    private static IXLWorkbook AbrirLibro(Stream contenido)
    {
        try
        {
            return new XLWorkbook(contenido);
        }
        catch (Exception excepcion) when (excepcion is not ErrorValidacion)
        {
            // Un archivo corrupto o que no es .xlsx es un error del usuario (400), no un 500.
            throw new ErrorValidacion(
                "El archivo no se pudo leer como Excel (.xlsx).",
                new[] { new DetalleError("archivo", "ilegible") });
        }
    }

    private static IReadOnlyList<string?> LeerCabecera(IXLWorksheet hoja)
        => Enumerable
            .Range(0, PlantillaParticipantes.Cabecera.Count)
            .Select(indice => PlantillaParticipantes.Normalizar(
                hoja.Row(1).Cell(indice + 1).GetString()))
            .ToArray();

    private static bool EstaVacia(IXLRow fila)
        => Enumerable
            .Range(0, PlantillaParticipantes.Cabecera.Count)
            .All(indice => string.IsNullOrWhiteSpace(fila.Cell(indice + 1).GetString()));

    private static string? Texto(IXLRow fila, int indiceColumna)
        => PlantillaParticipantes.Normalizar(fila.Cell(indiceColumna + 1).GetString());

    private static void LeerAntiguedad(IXLRow fila, out decimal? valor, out bool ilegible)
    {
        var celda = fila.Cell(PlantillaParticipantes.IndiceAntiguedad + 1);
        if (celda.DataType == XLDataType.Number && celda.Value.IsNumber)
        {
            // Valor crudo de la celda: evita el redondeo que impone el formato de presentacion.
            valor = (decimal)celda.Value.GetNumber();
            ilegible = false;
            return;
        }

        PlantillaParticipantes.ParsearAntiguedad(celda.GetString(), out valor, out ilegible);
    }
}
