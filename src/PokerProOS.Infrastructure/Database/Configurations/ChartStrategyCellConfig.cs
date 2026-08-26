using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using PokerProOS.Domain.Entities;

namespace PokerProOS.Infrastructure.Database.Configurations;

public class ChartStrategyCellConfig : IEntityTypeConfiguration<ChartStrategyCell>
{
    public void Configure(EntityTypeBuilder<ChartStrategyCell> builder)
    {
        builder.HasKey(e => e.Id);
        builder.HasIndex(e => new { e.SituationKey, e.StackKey, e.SpotKey, e.HandLabel }).IsUnique();
        builder.Property(e => e.SituationKey).HasMaxLength(50).IsRequired();
        builder.Property(e => e.SituationLabel).HasMaxLength(100).IsRequired();
        builder.Property(e => e.StackKey).HasMaxLength(20).IsRequired();
        builder.Property(e => e.SpotKey).HasMaxLength(50).IsRequired();
        builder.Property(e => e.SpotLabel).HasMaxLength(100).IsRequired();
        builder.Property(e => e.HandLabel).HasMaxLength(10).IsRequired();
        builder.Property(e => e.Action).HasMaxLength(20).IsRequired();
        builder.Property(e => e.Source).HasMaxLength(50).IsRequired();
        builder.Property(e => e.Version).HasMaxLength(20).IsRequired();
    }
}
