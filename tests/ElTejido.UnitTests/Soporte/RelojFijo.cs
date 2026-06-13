namespace ElTejido.UnitTests.Soporte;

/// <summary>
/// <see cref="TimeProvider"/> controlable para pruebas deterministas de logica con tiempo.
/// </summary>
internal sealed class RelojFijo : TimeProvider
{
    private DateTimeOffset _ahora;

    public RelojFijo(DateTimeOffset ahora)
    {
        _ahora = ahora;
    }

    public override DateTimeOffset GetUtcNow() => _ahora;

    public void Avanzar(TimeSpan delta) => _ahora = _ahora.Add(delta);
}
