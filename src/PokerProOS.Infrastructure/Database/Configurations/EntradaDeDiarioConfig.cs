using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using PokerProOS.Domain.Diario;

namespace PokerProOS.Infrastructure.Database.Configurations;

public class EntradaDeDiarioConfig : IEntityTypeConfiguration<EntradaDeDiario>
{
    public void Configure(EntityTypeBuilder<EntradaDeDiario> constructor)
    {
        constructor.HasKey(e => e.Id);
        // Una entrada por dia: guardar dos veces el mismo dia actualiza, no duplica.
        constructor.HasIndex(e => e.Fecha).IsUnique();
        constructor.Property(e => e.Intencion).HasMaxLength(300);
        constructor.Property(e => e.NivelDeJuego).HasMaxLength(1);
        constructor.Property(e => e.Disparador).HasMaxLength(300);
        // El cuerpo no se acota: es donde el usuario escribe de verdad.
        constructor.Property(e => e.Notas).IsRequired();
    }
}
