namespace PokerProOS.Domain.Tablas;

public record RangoDeStack(string Clave, decimal MinBB, decimal MaxBB)
{
    public bool Cubre(decimal bb) => bb >= MinBB && bb <= MaxBB;
}
