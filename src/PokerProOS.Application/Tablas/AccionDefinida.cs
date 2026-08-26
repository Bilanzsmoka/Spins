namespace PokerProOS.Application.Tablas;

public record AccionDefinida(
    string Clave,
    string Etiqueta,
    string Color,
    string ColorTexto,
    int Orden,
    IReadOnlyList<string> Dichos);
