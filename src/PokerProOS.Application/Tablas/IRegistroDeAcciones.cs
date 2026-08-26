namespace PokerProOS.Application.Tablas;

public interface IRegistroDeAcciones
{
    IReadOnlyList<AccionDefinida> Todas { get; }
    bool Existe(string clave);
    AccionDefinida Obtener(string clave);
}
