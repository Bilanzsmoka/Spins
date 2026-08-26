using Microsoft.EntityFrameworkCore;
using PokerProOS.Domain.Entities;

namespace PokerProOS.Infrastructure.Database;

public class PokerProOSDbContext : DbContext
{
    public DbSet<ChartStrategyCell> ChartStrategyCells => Set<ChartStrategyCell>();
    public DbSet<SpinSession> SpinSessions => Set<SpinSession>();
    public DbSet<SpinTournament> SpinTournaments => Set<SpinTournament>();
    public DbSet<TrainerAttempt> TrainerAttempts => Set<TrainerAttempt>();

    public PokerProOSDbContext(DbContextOptions<PokerProOSDbContext> options)
        : base(options) { }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(PokerProOSDbContext).Assembly);
    }
}
