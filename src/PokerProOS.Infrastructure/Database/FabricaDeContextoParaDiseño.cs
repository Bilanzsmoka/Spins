using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace PokerProOS.Infrastructure.Database;

/// <summary>
/// Permite que <c>dotnet ef</c> trabaje contra Infrastructure sin arrancar
/// el proyecto web. Sin esto, generar una migración exige compilar la Api,
/// y si la aplicación está corriendo sus dll están bloqueados y la
/// compilación falla — un roce que aparecía en cada cambio de esquema.
/// Solo se usa en tiempo de diseño; en ejecución manda Program.cs.
/// </summary>
public sealed class FabricaDeContextoParaDiseño : IDesignTimeDbContextFactory<PokerProOSDbContext>
{
    private const string ConexionPorDefecto =
        "Server=localhost;Database=PokerProOS;Trusted_Connection=True;TrustServerCertificate=True";

    public PokerProOSDbContext CreateDbContext(string[] args)
    {
        var conexion = Environment.GetEnvironmentVariable("POKERPROOS_CONEXION") ?? ConexionPorDefecto;
        var opciones = new DbContextOptionsBuilder<PokerProOSDbContext>()
            .UseSqlServer(conexion)
            .Options;
        return new PokerProOSDbContext(opciones);
    }
}
