using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using PokerProOS.Domain.Entities;

namespace PokerProOS.Infrastructure.Database.Configurations;

public class SpinTournamentConfig : IEntityTypeConfiguration<SpinTournament>
{
    public void Configure(EntityTypeBuilder<SpinTournament> builder)
    {
        builder.HasKey(e => e.Id);
        builder.HasIndex(e => e.TournamentId).IsUnique();
        builder.Property(e => e.Site).HasMaxLength(20).IsRequired();
        builder.Property(e => e.TournamentId).HasMaxLength(100).IsRequired();
        builder.Property(e => e.FileName).HasMaxLength(200);
        builder.Property(e => e.BuyIn).HasColumnType("decimal(18,2)");
        builder.Property(e => e.RawText).IsRequired();
    }
}
