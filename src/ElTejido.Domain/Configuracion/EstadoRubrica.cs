namespace ElTejido.Domain.Configuracion;

public enum EstadoRubrica
{
    Activa,
    Archivada,

    // Estado no comprometido: una rubrica en borrador nunca se usa para evaluar (el orquestador exige
    // Activa). Permite edicion in-place sin romper snapshots de evaluaciones pasadas. Se agrega al final
    // para conservar Activa=0 como valor por defecto del enum.
    Borrador,
}
