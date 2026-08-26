namespace PokerProOS.Application.Diario;

/// <summary>
/// Un hábito del cuadro diario. Vive en datos, no en columnas: agregar
/// "yoga" es una línea en database/registro/habitos.json, no una migración.
/// </summary>
/// <param name="Invertido">
/// Marcar que sí es lo malo, como el tilt. Cambia el color, no la mecánica.
/// </param>
public record HabitoDefinido(
    string Clave,
    string Etiqueta,
    string Tipo,
    int Orden,
    string Ayuda,
    bool Invertido);

public interface IRegistroDeHabitos
{
    IReadOnlyList<HabitoDefinido> Todos { get; }
    bool Existe(string clave);
}
