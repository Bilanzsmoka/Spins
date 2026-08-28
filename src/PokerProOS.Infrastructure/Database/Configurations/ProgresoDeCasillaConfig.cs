using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using PokerProOS.Domain.Entrenador;

namespace PokerProOS.Infrastructure.Database.Configurations;

public class ProgresoDeCasillaConfig : IEntityTypeConfiguration<ProgresoDeCasilla>
{
    public void Configure(EntityTypeBuilder<ProgresoDeCasilla> constructor)
    {
        constructor.HasKey(e => e.Id);
        constructor.Property(e => e.Situacion).HasMaxLength(50).IsRequired();
        constructor.Property(e => e.ClaveDeStack).HasMaxLength(20).IsRequired();
        constructor.Property(e => e.Spot).HasMaxLength(50).IsRequired();
        constructor.Property(e => e.Mano).HasMaxLength(10).IsRequired();

        // Unico: una casilla tiene un solo calendario por persona. Sin esto,
        // dos respuestas concurrentes dejarian dos filas y el progreso se
        // partiria en dos calendarios que se pisan.
        constructor
            .HasIndex(e => new { e.UsuarioId, e.Situacion, e.ClaveDeStack, e.Spot, e.Mano })
            .IsUnique();

        // La pregunta que arma cada tanda: que le vencio hoy a esta persona.
        constructor.HasIndex(e => new { e.UsuarioId, e.Vence });
    }
}
