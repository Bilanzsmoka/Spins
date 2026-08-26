using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using PokerProOS.Domain.Diario;

namespace PokerProOS.Infrastructure.Database.Configurations;

public class MarcaDeHabitoConfig : IEntityTypeConfiguration<MarcaDeHabito>
{
    public void Configure(EntityTypeBuilder<MarcaDeHabito> constructor)
    {
        constructor.HasKey(e => e.Id);
        // Una marca por habito por dia: volver a marcar actualiza, no duplica.
        constructor.HasIndex(e => new { e.Fecha, e.Clave }).IsUnique();
        constructor.Property(e => e.Clave).HasMaxLength(40).IsRequired();
        constructor.Property(e => e.Nota).HasMaxLength(400);
    }
}
