using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using PokerProOS.Domain.Bitacora;

namespace PokerProOS.Infrastructure.Database.Configurations;

public class ConsultaDeVozConfig : IEntityTypeConfiguration<ConsultaDeVoz>
{
    public void Configure(EntityTypeBuilder<ConsultaDeVoz> constructor)
    {
        constructor.HasKey(e => e.Id);
        constructor.Property(e => e.Situacion).HasMaxLength(50).IsRequired();
        constructor.Property(e => e.ClaveDeStack).HasMaxLength(20).IsRequired();
        constructor.Property(e => e.Spot).HasMaxLength(50).IsRequired();
        constructor.Property(e => e.Mano).HasMaxLength(10).IsRequired();
        constructor.Property(e => e.Accion).HasMaxLength(20).IsRequired();
        constructor.Property(e => e.TextoCrudo).HasMaxLength(500).IsRequired();
        // El indice sirve la pregunta que motiva la bitacora:
        // que manos consulto mas en cada spot.
        constructor.HasIndex(e => new { e.Situacion, e.ClaveDeStack, e.Spot, e.Mano });
    }
}
