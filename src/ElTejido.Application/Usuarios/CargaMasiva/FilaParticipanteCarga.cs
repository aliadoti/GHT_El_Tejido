namespace ElTejido.Application.Usuarios.CargaMasiva;

/// <summary>
/// Fila cruda de la plantilla oficial de GHT (I-08 §3), en el orden fijo de sus 9 columnas:
/// <c>Empresa | ID Empresa | Sede | Nombre | Cargo | Email | Antigüedad en la empresa en años |
/// Idioma | Telefono</c>.
/// <para>
/// El lector <b>no valida</b> los datos: solo separa columnas, recorta espacios y convierte a
/// <c>null</c> lo vacio. Quien decide si la fila entra es <c>ServicioCargaMasiva</c>, para que
/// <c>.xlsx</c> y <c>.csv</c> se comporten igual ante los mismos contenidos.
/// </para>
/// <para>
/// <c>codigoUsuario</c> y <c>usuarioWhatsapp</c> no viven aqui a proposito: nunca se leen del archivo
/// (03 §3.1). Tampoco hay columna <c>Tags</c>; la tag de empresa se deriva de <see cref="EmpresaId"/>.
/// </para>
/// </summary>
/// <param name="Fila">Numero de fila en el archivo (1-based, la cabecera es la fila 1).</param>
/// <param name="AntiguedadAnios">
/// Antiguedad ya tipada, sin redondear. Es <c>null</c> tanto si la celda venia vacia como si el texto
/// no era un numero; para distinguir los dos casos esta <see cref="AntiguedadIlegible"/>.
/// </param>
/// <param name="AntiguedadIlegible">
/// La celda traia algo que no es un numero. El servicio lo traduce a <c>antiguedad_invalida</c> en vez
/// de tragarse el dato en silencio.
/// </param>
public sealed record FilaParticipanteCarga(
    int Fila,
    string? Empresa,
    string? EmpresaId,
    string? Sede,
    string? Nombre,
    string? Cargo,
    string? Email,
    decimal? AntiguedadAnios,
    bool AntiguedadIlegible,
    string? Idioma,
    string? Telefono);
