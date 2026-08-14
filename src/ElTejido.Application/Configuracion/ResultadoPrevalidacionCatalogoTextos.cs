using ElTejido.Application.Common;

namespace ElTejido.Application.Configuracion;

/// <summary>DT-P32-02 §3.3: conteos para revision humana; nunca incluye textos.</summary>
public sealed record ConteosCatalogoTextos(int Mensajes, int GruposFrases, int Frases);

/// <summary>
/// DT-P32-02 §3.3: resultado de prevalidar un catalogo sin escribir. Devuelve todos los errores
/// detectables en una sola pasada, con el mismo validador que usa la escritura real.
/// </summary>
public sealed record ResultadoPrevalidacionCatalogoTextos(
    bool Valido,
    string FamiliaId,
    string Idioma,
    ConteosCatalogoTextos Conteos,
    IReadOnlyList<DetalleError> Errores);

/// <summary>DT-P32-02 §3.2: forma canonica del JSON editable del catalogo.</summary>
public static class FormatoCatalogoTextos
{
    public const string V1 = "catalogo-textos/v1";
}
