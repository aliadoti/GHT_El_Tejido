namespace ElTejido.Calibracion;

/// <summary>Error de tooling de calibración (D5): golden set inválido, baseline corrupto, etc.
/// Es una herramienta de QA fuera de la ruta de request; no se traduce al modelo de error de 04 §3.</summary>
public sealed class CalibracionException : Exception
{
    public CalibracionException(string message)
        : base(message)
    {
    }

    public CalibracionException(string message, Exception inner)
        : base(message, inner)
    {
    }
}
