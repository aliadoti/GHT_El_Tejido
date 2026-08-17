using ElTejido.Domain.Localizacion;

namespace ElTejido.Application.Evaluacion;

public enum TipoDirectivaIdiomaLlm
{
    Orientativo,
    Salida,
    SalidaObligatoria,
}

public abstract record ResultadoDirectivaIdiomaLlm
{
    public sealed record Disponible(IdiomaConversacion Idioma, string Directiva)
        : ResultadoDirectivaIdiomaLlm;

    public sealed record NoDisponible(string Codigo)
        : ResultadoDirectivaIdiomaLlm;
}

/// <summary>Produce solo la directiva de idioma; no traduce prompts ni decide negocio.</summary>
public interface IPoliticaIdiomaLlm
{
    ResultadoDirectivaIdiomaLlm Resolver(string idioma, TipoDirectivaIdiomaLlm tipo);
}

public sealed class PoliticaIdiomaLlm : IPoliticaIdiomaLlm
{
    public const string CodigoIdiomaNoSoportado = "idioma_no_soportado";

    public ResultadoDirectivaIdiomaLlm Resolver(string idioma, TipoDirectivaIdiomaLlm tipo)
    {
        if (!IdiomaConversacion.TryCrear(idioma, out var idiomaConversacion))
        {
            return new ResultadoDirectivaIdiomaLlm.NoDisponible(CodigoIdiomaNoSoportado);
        }

        var etiqueta = tipo switch
        {
            TipoDirectivaIdiomaLlm.Orientativo => "IDIOMA_ORIENTATIVO",
            TipoDirectivaIdiomaLlm.SalidaObligatoria => "IDIOMA_DE_SALIDA_OBLIGATORIO",
            _ => "IDIOMA_DE_SALIDA",
        };
        return new ResultadoDirectivaIdiomaLlm.Disponible(
            idiomaConversacion,
            $"{etiqueta}: {idiomaConversacion.Codigo}");
    }

    public static string Requerir(
        IPoliticaIdiomaLlm politica,
        string idioma,
        TipoDirectivaIdiomaLlm tipo)
        => politica.Resolver(idioma, tipo) is ResultadoDirectivaIdiomaLlm.Disponible disponible
            ? disponible.Directiva
            : throw new InvalidOperationException("El contexto LLM contiene un idioma no soportado.");
}
