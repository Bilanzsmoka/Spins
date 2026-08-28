using PokerProOS.Application.Entrenador;

namespace PokerProOS.Tests.Entrenador;

/// <summary>
/// La escalera de intervalos, sin base ni reloj: la fecha entra como
/// parámetro para que las pruebas no dependan del día en que se corren.
/// </summary>
public class CalendarioDeRepeticionTests
{
    private static readonly DateOnly Hoy = new(2026, 8, 28);

    [Theory]
    [InlineData(0, 1, 1)]   // primera vez que se acierta: descansa 1 día
    [InlineData(1, 2, 3)]
    [InlineData(2, 3, 7)]
    [InlineData(3, 4, 16)]
    [InlineData(4, 5, 35)]
    [InlineData(5, 6, 90)]
    public void Acertar_sube_un_escalon(int previos, int esperadosAciertos, int esperadoIntervalo)
    {
        var p = CalendarioDeRepeticion.Siguiente(previos, acerto: true, Hoy);

        Assert.Equal(esperadosAciertos, p.AciertosSeguidos);
        Assert.Equal(esperadoIntervalo, p.IntervaloEnDias);
        Assert.Equal(Hoy.AddDays(esperadoIntervalo), p.Vence);
    }

    /// <summary>
    /// Arriba de la escalera se queda en el último escalón. Sin este tope,
    /// el índice se saldría del arreglo en el séptimo acierto.
    /// </summary>
    [Fact]
    public void Sobre_el_ultimo_escalon_el_intervalo_no_crece_mas()
    {
        var p = CalendarioDeRepeticion.Siguiente(12, acerto: true, Hoy);

        Assert.Equal(13, p.AciertosSeguidos);
        Assert.Equal(90, p.IntervaloEnDias);
    }

    /// <summary>
    /// Fallar no baja un escalón: vuelve a cero. Media memoria de una casilla
    /// no es memoria, y el spec pide además que reentra en la tanda actual,
    /// por eso vence HOY y no mañana.
    /// </summary>
    [Fact]
    public void Fallar_resetea_y_vence_hoy()
    {
        var p = CalendarioDeRepeticion.Siguiente(5, acerto: false, Hoy);

        Assert.Equal(0, p.AciertosSeguidos);
        Assert.Equal(1, p.IntervaloEnDias);
        Assert.Equal(Hoy, p.Vence);
    }

    [Fact]
    public void La_escalera_es_la_del_spec()
        => Assert.Equal(new[] { 1, 3, 7, 16, 35, 90 }, CalendarioDeRepeticion.Escalera);
}
