namespace ElTejido.Domain.Identidad;

/// <summary>
/// Normaliza numeros de WhatsApp para comparaciones de identidad, auth y matricula.
/// Cumple REQ 10.2, REQ 12.2.2 y ARQ 16.
/// </summary>
public interface INormalizadorNumero
{
    NumeroWhatsApp Normalizar(string numeroCrudo);

    bool TryNormalizar(string numeroCrudo, out NumeroWhatsApp? numero);
}

