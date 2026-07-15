using System.Text;
using ElTejido.Application.Common;

namespace ElTejido.Application.Usuarios.CargaMasiva;

/// <summary>
/// Lector CSV de la plantilla de carga masiva (I-08), sin dependencias externas (Sprint 1a). Interpreta
/// comillas dobles al estilo RFC 4180 (campos entrecomillados, comas y saltos de linea dentro de
/// comillas, <c>""</c> como comilla escapada) y ambos finales de linea (<c>\n</c>/<c>\r\n</c>). La
/// plantilla tiene cabecera obligatoria y columnas fijas por posicion:
/// <c>Nombre, WhatsApp, Area, Empresa, Tags</c> (las <c>Tags</c> se separan con <c>;</c>).
/// </summary>
public sealed class LectorCsvParticipantes : ILectorArchivoParticipantes
{
    private static readonly string[] Cabecera = { "Nombre", "WhatsApp", "Area", "Empresa", "Tags" };

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

        ValidarCabecera(registros[0]);

        var filas = new List<FilaParticipanteCarga>(registros.Count - 1);
        for (var indice = 1; indice < registros.Count; indice++)
        {
            var campos = registros[indice];
            if (campos.All(string.IsNullOrWhiteSpace))
            {
                continue; // Linea en blanco: se ignora pero conserva el numero de fila del archivo.
            }

            var numeroFila = indice + 1; // La cabecera es la fila 1 (1-based, como en una hoja de calculo).
            filas.Add(new FilaParticipanteCarga(
                numeroFila,
                Columna(campos, 0),
                Columna(campos, 1),
                Columna(campos, 2),
                Columna(campos, 3),
                ParsearTags(Columna(campos, 4))));
        }

        return filas;
    }

    private static void ValidarCabecera(IReadOnlyList<string> cabecera)
    {
        var valida = cabecera.Count >= Cabecera.Length
            && Cabecera
                .Select((nombre, indice) => string.Equals(cabecera[indice].Trim(), nombre, StringComparison.OrdinalIgnoreCase))
                .All(coincide => coincide);

        if (!valida)
        {
            throw new ErrorValidacion(
                "La cabecera del archivo no coincide con la plantilla (Nombre, WhatsApp, Area, Empresa, Tags).",
                new[] { new DetalleError("cabecera", "invalida") });
        }
    }

    private static string? Columna(IReadOnlyList<string> campos, int indice)
    {
        if (indice >= campos.Count)
        {
            return null;
        }

        var valor = campos[indice].Trim();
        return valor.Length == 0 ? null : valor;
    }

    private static IReadOnlyCollection<string> ParsearTags(string? valor)
    {
        if (string.IsNullOrWhiteSpace(valor))
        {
            return Array.Empty<string>();
        }

        return valor
            .Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Distinct(StringComparer.Ordinal)
            .ToArray();
    }

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
