using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using PokerProOS.Domain.Entrenador;

namespace PokerProOS.Infrastructure.Database.Configurations;

public class RespuestaRegistradaConfig : IEntityTypeConfiguration<RespuestaRegistrada>
{
    public void Configure(EntityTypeBuilder<RespuestaRegistrada> builder)
    {
        builder.HasKey(r => r.Id);
        builder.Property(r => r.Situacion).HasMaxLength(80).IsRequired();
        builder.Property(r => r.ClaveDeStack).HasMaxLength(20).IsRequired();
        builder.Property(r => r.Spot).HasMaxLength(60).IsRequired();
        builder.Property(r => r.Mano).HasMaxLength(4).IsRequired();
        builder.Property(r => r.AccionElegida).HasMaxLength(30).IsRequired();
        builder.Property(r => r.AccionCorrecta).HasMaxLength(30).IsRequired();

        // Las dos preguntas que esta tabla existe para contestar: qué fallo se
        // repite —por casilla— y cómo viene la velocidad en el tiempo.
        builder.HasIndex(r => new { r.UsuarioId, r.Situacion, r.ClaveDeStack, r.Spot, r.Mano });
        builder.HasIndex(r => new { r.UsuarioId, r.RespondidaEn });
    }
}
