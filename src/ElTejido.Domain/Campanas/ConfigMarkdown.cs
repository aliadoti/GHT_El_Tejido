namespace ElTejido.Domain.Campanas;

public sealed class ConfigMarkdown
{
    private ConfigMarkdown(TipoArtefactoMarkdown tipoArtefacto)
    {
        TipoArtefacto = tipoArtefacto;
    }

    public TipoArtefactoMarkdown TipoArtefacto { get; }

    public static ConfigMarkdown Crear(TipoArtefactoMarkdown tipoArtefacto)
    {
        return new ConfigMarkdown(tipoArtefacto);
    }
}
