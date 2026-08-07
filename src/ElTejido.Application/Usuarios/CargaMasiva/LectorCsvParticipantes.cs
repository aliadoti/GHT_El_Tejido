using System.Text;
using ElTejido.Application.Common;

namespace ElTejido.Application.Usuarios.CargaMasiva;

/// <summary>
/// Lector CSV de la plantilla oficial de GHT (I-08 §3), sin dependencias externas. Interpreta comillas
/// dobles al estilo RFC 4180 (campos entrecomillados, comas y saltos de linea dentro de comillas,
/// <c>""</c> como comilla escapada) y ambos finales de linea (<c>\n</c>/<c>\r\n</c>). Cabecera
/// obligatoria y 9 columnas fijas por posicion; los nombres y su validacion viven en
/// <see cref="PlantillaParticipantes"/>, compartidos con el lector <c>.xlsx</c>.
/// <para>Es el formato de respaldo: el primario es el <c>.xlsx</c> que entrega GHT (I-08 §10).</para>
/// </summary>
public sealed class LectorCsvParticipantes : ILectorArchivoParticipantes
{
    public bool Soporta(string extensionArchivo)
        => string.Equals(extensionArchivo, ".csv", StringComparison.OrdinalIgnoreCase);

    public async Task<IReadOnlyList<FilaParticipanteCarga>> LeerAsync(
        Stream contenido,
        CancellationToken cancellationToken)
    {
        // UTF-8 con deteccion de BOM (Excel exporta CSV con BOM con frecuencia).
        using var lector = new StreamReader(contenido, Encoding.UTF8, detectEncodingFromByteOrderMarks: true);
        var texto = await lector.ReadToEndAsync(cancellationToken);

        var registros = ParsearRegistros(texto);
        if (registros.Count == 0)
        {
            throw new ErrorValidacion(
                "El archivo esta vacio.",
                new[] { new DetalleError("archivo", "vacio") });
        }

        PlantillaParticipantes.ValidarCabecera(registros[0]);

        var filas = new List<FilaParticipanteCarga>(registros.Count - 1);
        for (var indice = 1; indice < registros.Count; indice++)
        {
            var campos = registros[indice];
            if (campos.All(string.IsNullOrWhiteSpace))
            {
                // Fila totalmente vacia: se descarta (la V1 de GHT trae una) sin correr la numeracion.
                continue;
            }

            PlantillaParticipantes.ParsearAntiguedad(
                Columna(campos, PlantillaParticipantes.IndiceAntiguedad),
                out var antiguedad,
                out var antiguedadIlegible);

            var numeroFila = indice + 1; // La cabecera es la fila 1 (1-based, como en una hoja de calculo).
            filas.Add(new FilaParticipanteCarga(
                numeroFila,
                Columna(campos, PlantillaParticipantes.IndiceEmpresa),
                Columna(campos, PlantillaParticipantes.IndiceEmpresaId),
                Columna(campos, PlantillaParticipantes.IndiceSede),
                Columna(campos, PlantillaParticipantes.IndiceNombre),
                Columna(campos, PlantillaParticipantes.IndiceCargo),
                Columna(campos, PlantillaParticipantes.IndiceEmail),
                antiguedad,
                antiguedadIlegible,
                Columna(campos, PlantillaParticipantes.IndiceIdioma),
                Columna(campos, PlantillaParticipantes.IndiceTelefono)));
        }

        return filas;
    }

    private static string? Columna(IReadOnlyList<string> campos, int indice)
        => indice >= campos.Count ? null : PlantillaParticipantes.Normalizar(campos[indice]);

    // Maquina de estados minima RFC 4180: separa el texto en registros (filas) y campos (columnas),
    // respetando comillas. Devuelve un registro por fila del archivo, en orden, incluidas las vacias.
    private static List<List<string>> ParsearRegistros(string texto)
    {
        var registros = new List<List<string>>();
        var registroActual = new List<string>();
        var campo = new StringBuilder();
        var enComillas = false;
        var registroTieneContenido = false;

        void CerrarCampo()
        {
            registroActual.Add(campo.ToString());
            campo.Clear();
        }

        void CerrarRegistro()
        {
            CerrarCampo();
            registros.Add(registroActual);
            registroActual = new List<string>();
            registroTieneContenido = false;
        }

        for (var i = 0; i < texto.Length; i++)
        {
            var caracter = texto[i];
            if (enComillas)
            {
                if (caracter == '"')
                {
                    if (i + 1 < texto.Length && texto[i + 1] == '"')
                    {
                        campo.Append('"');
                        i++; // Comilla escapada ("").
                    }
                    else
                    {
                        enComillas = false;
                    }
                }
                else
                {
                    campo.Append(caracter);
                }

                continue;
            }

            switch (caracter)
            {
                case '"':
                    enComillas = true;
                    registroTieneContenido = true;
                    break;
                case ',':
                    CerrarCampo();
                    registroTieneContenido = true;
                    break;
                case '\r':
                    // Se ignora; el salto de linea lo marca el \n (o el fin de archivo abajo).
                    if (i + 1 >= texto.Length || texto[i + 1] != '\n')
                    {
                        CerrarRegistro();
                    }

                    break;
                case '\n':
                    CerrarRegistro();
                    break;
                default:
                    campo.Append(caracter);
                    registroTieneContenido = true;
                    break;
            }
        }

        // Ultimo registro sin salto de linea final (o resto pendiente).
        if (registroTieneContenido || campo.Length > 0 || registroActual.Count > 0)
        {
            CerrarRegistro();
        }

        return registros;
    }
}
