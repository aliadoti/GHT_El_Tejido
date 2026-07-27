namespace ElTejido.Domain.Evaluacion;

/// <summary>Registro reproducible y acotado de las semillas disponibles para una evaluación I-19.</summary>
public sealed record SeedThoughtsSnapshot(bool Usadas, IReadOnlyCollection<string> Contenido, bool Truncadas)
{
    public static SeedThoughtsSnapshot Vacio { get; } = new(false, Array.Empty<string>(), false);
}
