using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using PokerProOS.Domain.Entities;

namespace PokerProOS.Infrastructure.Database.Configurations;

public class TrainerAttemptConfig : IEntityTypeConfiguration<TrainerAttempt>
{
    public void Configure(EntityTypeBuilder<TrainerAttempt> builder)
    {
        builder.HasKey(e => e.Id);
        builder.Property(e => e.Pack).HasMaxLength(50).IsRequired();
        builder.Property(e => e.Format).HasMaxLength(20).IsRequired();
        builder.Property(e => e.Spot).HasMaxLength(50).IsRequired();
        builder.Property(e => e.Villain).HasMaxLength(50).IsRequired();
        builder.Property(e => e.HandLabel).HasMaxLength(10).IsRequired();
        builder.Property(e => e.ExpectedAction).HasMaxLength(20).IsRequired();
        builder.Property(e => e.ChosenAction).HasMaxLength(20).IsRequired();
        builder.Property(e => e.Score).HasColumnType("decimal(18,2)");
        builder.Property(e => e.Adjustment).HasColumnType("decimal(18,2)");
    }
}
