using Microsoft.EntityFrameworkCore;
using PokerProOS.Domain.Bitacora;
using PokerProOS.Domain.Diario;
using PokerProOS.Domain.Entities;
using PokerProOS.Domain.Entrenador;

namespace PokerProOS.Infrastructure.Database;

public class PokerProOSDbContext : DbContext
{
    public DbSet<ChartStrategyCell> ChartStrategyCells => Set<ChartStrategyCell>();
    public DbSet<SpinSession> SpinSessions => Set<SpinSession>();
    public DbSet<SpinTournament> SpinTournaments => Set<SpinTournament>();
    public DbSet<TrainerAttempt> TrainerAttempts => Set<TrainerAttempt>();
    public DbSet<ConsultaDeVoz> ConsultasDeVoz => Set<ConsultaDeVoz>();
    public DbSet<EntradaDeDiario> EntradasDeDiario => Set<EntradaDeDiario>();
    public DbSet<MarcaDeHabito> MarcasDeHabito => Set<MarcaDeHabito>();
    public DbSet<ProgresoDeCasilla> ProgresosDeCasilla => Set<ProgresoDeCasilla>();
    public DbSet<RespuestaRegistrada> RespuestasRegistradas => Set<RespuestaRegistrada>();

    public PokerProOSDbContext(DbContextOptions<PokerProOSDbContext> options)
        : base(options) { }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(PokerProOSDbContext).Assembly);
    }
}
