using PokerProOS.Domain.Manos;

namespace PokerProOS.Tests.Manos;

public class MatrizDeManosTests
{
    [Fact]
    public void Genera_exactamente_169_manos()
        => Assert.Equal(169, MatrizDeManos.Todas().Count);

    [Fact]
    public void No_repite_ninguna_mano()
    {
        var todas = MatrizDeManos.Todas();
        Assert.Equal(todas.Count, todas.Distinct().Count());
    }

    [Fact]
    public void Contiene_13_parejas()
        => Assert.Equal(13, MatrizDeManos.Todas().Count(m => m.Length == 2));

    [Theory]
    [InlineData(0, 0, "AA")]
    [InlineData(0, 1, "AKs")]
    [InlineData(1, 0, "AKo")]
    [InlineData(12, 12, "22")]
    [InlineData(4, 9, "T5s")]
    [InlineData(9, 4, "T5o")]
    public void Ubica_la_mano_en_la_celda_correcta(int fila, int columna, string esperada)
        => Assert.Equal(esperada, MatrizDeManos.Etiqueta(fila, columna));

    [Fact]
    public void Las_vecinas_de_una_celda_interior_son_cuatro()
    {
        var vecinas = MatrizDeManos.Vecinas("T5s");
        Assert.Equal(4, vecinas.Count);
        Assert.Contains("J5s", vecinas);
        Assert.Contains("95s", vecinas);
        Assert.Contains("T6s", vecinas);
        Assert.Contains("T4s", vecinas);
    }

    [Fact]
    public void Las_vecinas_de_una_esquina_son_dos()
        => Assert.Equal(2, MatrizDeManos.Vecinas("AA").Count);

    [Fact]
    public void Toda_mano_generada_tiene_vecinas_validas()
    {
        var todas = MatrizDeManos.Todas().ToHashSet();
        foreach (var mano in todas)
            Assert.All(MatrizDeManos.Vecinas(mano), v => Assert.Contains(v, todas));
    }
}
