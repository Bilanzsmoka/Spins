using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using PokerProOS.Domain.Entities;

namespace PokerProOS.Infrastructure.Database.Configurations;

public class SpinSessionConfig : IEntityTypeConfiguration<SpinSession>
{
    public void Configure(EntityTypeBuilder<SpinSession> builder)
    {
        builder.HasKey(e => e.Id);
        builder.Property(e => e.Stake).HasMaxLength(20).IsRequired();
        builder.Property(e => e.BuyIn).HasColumnType("decimal(18,2)");
        builder.Property(e => e.PrizeTotal).HasColumnType("decimal(18,2)");
        builder.Property(e => e.NetResult).HasColumnType("decimal(18,2)");
        builder.Property(e => e.Rakeback).HasColumnType("decimal(18,2)");
        builder.Property(e => e.PromoValue).HasColumnType("decimal(18,2)");
        builder.Property(e => e.ChipEvTotal).HasColumnType("decimal(18,2)");
    }
}
