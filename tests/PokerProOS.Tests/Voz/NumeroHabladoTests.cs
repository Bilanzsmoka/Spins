using PokerProOS.Application.Voz;

namespace PokerProOS.Tests.Voz;

public class NumeroHabladoTests
{
    [Theory]
    [InlineData("9", 9)]
    [InlineData("15", 15)]
    [InlineData("nueve", 9)]
    [InlineData("quince", 15)]
    [InlineData("veinte", 20)]
    [InlineData("veintitres", 23)]
    [InlineData("treinta y cinco", 35)]
    [InlineData("noventa y nueve", 99)]
    [InlineData("uno", 1)]
    public void Interpreta_numeros_en_digitos_y_en_palabras(string texto, int esperado)
        => Assert.Equal(esperado, NumeroHablado.Interpretar(texto));

    [Theory]
    [InlineData("")]
    [InlineData("limp")]
    [InlineData("cuba")]
    [InlineData("0")]
    [InlineData("100")]
    [InlineData("ciento veinte")]
    public void Devuelve_nulo_cuando_no_es_un_numero_de_stack(string texto)
        => Assert.Null(NumeroHablado.Interpretar(texto));
}
