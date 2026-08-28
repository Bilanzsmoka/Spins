using PokerProOS.Application.Entrenador;
using PokerProOS.Application.Tablas;
using PokerProOS.Domain.Entrenador;
using PokerProOS.Domain.Manos;
using PokerProOS.Domain.Tablas;
using PokerProOS.Infrastructure.Tablas;

namespace PokerProOS.Tests.Entrenador;

public class ResponderRespuestaHandlerTests
{
    private static readonly DateOnly Hoy = new(2026, 8, 28);

    /// <summary>
    /// AA es un mix mitad y mitad; KK es ALL-IN puro; el resto FOLD. Con eso
    /// alcanza para acierto, fallo y mano mixta.
    /// </summary>
    private static ICatalogoDeTablas Catalogo()
    {
        var celdas = MatrizDeManos.Todas().Select(m => m switch
        {
            "AA" => new CeldaDeTabla(m, "ALL-IN",
                [new ParteDeMix("ALL-IN", 50), new ParteDeMix("CALL", 50)]),
            "KK" => new CeldaDeTabla(m, "ALL-IN"),
            _ => new CeldaDeTabla(m, "FOLD"),
        }).ToList();

        return new CatalogoEnMemoria(
            [
                new SituacionDeTabla("HU_X", "HU equis | fish", "HU",
                [
                    new TablaDeStack(new RangoDeStack("9-11bb", 9, 11),
                    [
                        new SpotDeTabla("SB_OR", "SB abre", celdas),
                    ]),
                ]),
            ], []);
    }

    private sealed class ProgresoEnMemoria : IProgresoDeEntrenamiento
    {
        public List<ProgresoDeCasilla> Filas { get; } = [];

        public Task<IReadOnlyList<ProgresoDeCasilla>> VencidasAsync(
            int usuarioId, DateOnly hoy, CancellationToken ct)
            => Task.FromResult<IReadOnlyList<ProgresoDeCasilla>>(
                Filas.Where(f => f.UsuarioId == usuarioId && f.Vence <= hoy).ToList());

        public Task<IReadOnlyList<ProgresoDeCasilla>> TodasAsync(int usuarioId, CancellationToken ct)
            => Task.FromResult<IReadOnlyList<ProgresoDeCasilla>>(
                Filas.Where(f => f.UsuarioId == usuarioId).ToList());

        public Task<ProgresoDeCasilla?> BuscarAsync(
            int usuarioId, string situacion, string claveDeStack, string spot, string mano,
            CancellationToken ct)
            => Task.FromResult(Filas.FirstOrDefault(f =>
                f.UsuarioId == usuarioId && f.Situacion == situacion
                && f.ClaveDeStack == claveDeStack && f.Spot == spot && f.Mano == mano));

        public Task GuardarAsync(ProgresoDeCasilla progreso, CancellationToken ct)
        {
            if (!Filas.Contains(progreso)) Filas.Add(progreso);
            return Task.CompletedTask;
        }
    }

    private static (ResponderRespuestaHandler Handler, ProgresoEnMemoria Progreso) Armar()
    {
        var catalogo = Catalogo();
        var progreso = new ProgresoEnMemoria();
        return (new ResponderRespuestaHandler(
            new ResolverManoHandler(catalogo),
            new AnalizadorDeMemoria(catalogo),
            catalogo,
            progreso), progreso);
    }

    private static RespuestaEnviada Enviada(string mano, string accion)
        => new("HU_X", "9-11bb", "SB_OR", mano, accion);

    [Fact]
    public async Task Acertar_avanza_el_calendario_y_no_trae_ficha()
    {
        var (handler, progreso) = Armar();

        var v = await handler.ResponderAsync(1, Enviada("KK", "ALL-IN"), Hoy, default);

        Assert.True(v!.Acerto);
        Assert.Null(v.Ficha);
        Assert.Equal(Hoy.AddDays(1), v.Vence);
        Assert.Equal(1, progreso.Filas.Single().AciertosSeguidos);
    }

    /// <summary>
    /// Al fallar viene la ficha entera: es el momento en que más sirve, y es
    /// justo el que el entrenador de PokerHero desaprovecha.
    /// </summary>
    [Fact]
    public async Task Fallar_trae_la_ficha_y_vuelve_a_vencer_hoy()
    {
        var (handler, progreso) = Armar();

        var v = await handler.ResponderAsync(1, Enviada("KK", "FOLD"), Hoy, default);

        Assert.False(v!.Acerto);
        Assert.Equal("ALL-IN", v.AccionCorrecta);
        Assert.NotNull(v.Ficha);
        Assert.Equal("KK", v.Ficha.Mano);
        Assert.Equal(Hoy, v.Vence);
        Assert.Equal(0, progreso.Filas.Single().AciertosSeguidos);
    }

    /// <summary>
    /// Una mano mixta cuenta por cualquiera de sus partes: elegir una como "la
    /// correcta" sería inventar una estrategia que la tabla no declara.
    /// </summary>
    [Theory]
    [InlineData("ALL-IN")]
    [InlineData("CALL")]
    public async Task Una_mano_mixta_acepta_las_dos_partes(string accion)
    {
        var (handler, _) = Armar();

        var v = await handler.ResponderAsync(1, Enviada("AA", accion), Hoy, default);

        Assert.True(v!.Acerto);
        Assert.NotNull(v.Mix);
        Assert.Equal(2, v.Mix.Count);
    }

    [Fact]
    public async Task Una_accion_que_no_es_del_mix_falla()
    {
        var (handler, _) = Armar();

        var v = await handler.ResponderAsync(1, Enviada("AA", "FOLD"), Hoy, default);

        Assert.False(v!.Acerto);
    }

    /// <summary>
    /// Acertar dos veces seguidas sube dos escalones: el handler tiene que
    /// leer el progreso previo, no arrancar de cero cada vez.
    /// </summary>
    [Fact]
    public async Task Dos_aciertos_seguidos_suben_dos_escalones()
    {
        var (handler, _) = Armar();

        await handler.ResponderAsync(1, Enviada("KK", "ALL-IN"), Hoy, default);
        var v = await handler.ResponderAsync(1, Enviada("KK", "ALL-IN"), Hoy, default);

        Assert.Equal(Hoy.AddDays(3), v!.Vence);
    }

    [Fact]
    public async Task Una_casilla_que_no_existe_devuelve_null()
    {
        var (handler, _) = Armar();

        var v = await handler.ResponderAsync(
            1, new RespuestaEnviada("NO_EXISTE", "9-11bb", "SB_OR", "KK", "FOLD"), Hoy, default);

        Assert.Null(v);
    }
}
