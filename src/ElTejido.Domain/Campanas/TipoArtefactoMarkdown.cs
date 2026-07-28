namespace ElTejido.Domain.Campanas;

public enum TipoArtefactoMarkdown
{
    Respuesta,
    Participante,
    Campania,
    Entidad,
    Capitulo,

    // I-19 (03 §3.10, §10 de la iniciativa): artefacto canonico por idea logica. Aditivo al final para
    // preservar los valores existentes; los artefactos historicos por respuesta se conservan.
    Idea,
}
