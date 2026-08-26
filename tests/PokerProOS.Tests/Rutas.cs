namespace PokerProOS.Tests;

/// <summary>
/// Ubica la carpeta database/ subiendo desde el directorio de salida
/// hasta encontrar la raíz del repositorio. Evita rutas relativas frágiles
/// como las cinco subidas que tenía el sembrado original.
/// </summary>
public static class Rutas
{
    private static readonly string RaizRepo = Localizar();

    public static string Registro(string archivo)
        => Path.Combine(RaizRepo, "database", "registro", archivo);

    public static string SemillasDeTablas
        => Path.Combine(RaizRepo, "database", "seed-data");

    private static string Localizar()
    {
        var actual = new DirectoryInfo(AppContext.BaseDirectory);
        while (actual is not null)
        {
            if (Directory.Exists(Path.Combine(actual.FullName, "database")))
                return actual.FullName;
            actual = actual.Parent;
        }
        throw new DirectoryNotFoundException(
            "No se encontró la carpeta database/ subiendo desde " + AppContext.BaseDirectory);
    }
}
