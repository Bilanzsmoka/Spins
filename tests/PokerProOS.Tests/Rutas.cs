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
            // La solución es la marca de la raíz, no database/ a secas: desde
            // que las pruebas referencian a Api, sus json de database/ se
            // copian también a bin/ de las pruebas, y esa copia le ganaba a la
            // del repo. Peor: ahí nadie borra los sobrantes, así que una tabla
            // eliminada seguiría validándose para siempre.
            if (File.Exists(Path.Combine(actual.FullName, "PokerProOS.slnx"))
                && Directory.Exists(Path.Combine(actual.FullName, "database")))
                return actual.FullName;
            actual = actual.Parent;
        }
        throw new DirectoryNotFoundException(
            "No se encontró la raíz del repositorio subiendo desde " + AppContext.BaseDirectory);
    }
}
