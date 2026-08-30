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

    private sealed class BitacoraEnMemoria : IBitacoraDeRespuestas
    {
        public List<RespuestaRegistrada> Filas { get; } = [];

        public Task RegistrarAsync(RespuestaRegistrada respuesta, CancellationToken ct)
        {
            Filas.Add(respuesta);
            return Task.CompletedTask;
        }
    }

    private static (ResponderRespuestaHandler Handler, ProgresoEnMemoria Progreso) Armar()
        => Armar(new BitacoraEnMemoria());

    private static (ResponderRespuestaHandler Handler, ProgresoEnMemoria Progreso) Armar(
        IBitacoraDeRespuestas bitacora)
    {
        var catalogo = Catalogo();
        var progreso = new ProgresoEnMemoria();
        return (new ResponderRespuestaHandler(
            new ResolverManoHandler(catalogo),
            new AnalizadorDeMemoria(catalogo),
            catalogo,
            progreso,
            bitacora), progreso);
    }

    private static RespuestaEnviada Enviada(string mano, string accion, int ms = 0)
        => new("HU_X", "9-11bb", "SB_OR", mano, accion, ms);

    /* ---------- La bitácora ---------- */

    /// <summary>
    /// El calendario guarda el estado y se pisa cada vez; la bitácora guarda el
    /// hecho y no se pisa nunca. Sin el hecho no hay forma de saber después qué
    /// se erró ni cuánto se tardó, que es de lo que dependen el mapa de errores
    /// y la curva de velocidad.
    /// </summary>
    [Fact]
    public async Task Cada_respuesta_queda_registrada_con_lo_que_contestaste_y_cuanto_tardaste()
    {
        var bitacora = new BitacoraEnMemoria();
        var (handler, _) = Armar(bitacora);

        await handler.ResponderAsync(7, Enviada("KK", "FOLD", ms: 2400), Hoy, default);

        var fila = Assert.Single(bitacora.Filas);
        Assert.Equal(7, fila.UsuarioId);
        Assert.Equal("KK", fila.Mano);
        Assert.Equal("FOLD", fila.AccionElegida);
        Assert.Equal("ALL-IN", fila.AccionCorrecta);
        Assert.False(fila.Acerto);
        Assert.Equal(2400, fila.Milisegundos);
    }

    /// <summary>
    /// Acertar también se registra. Si sólo se guardaran los fallos no se
    /// podría saber si estás contestando más rápido que el mes pasado, que es
    /// justamente lo que distingue saber una tabla de tenerla como reflejo.
    /// </summary>
    [Fact]
    public async Task Acertar_tambien_deja_registro()
    {
        var bitacora = new BitacoraEnMemoria();
        var (handler, _) = Armar(bitacora);

        await handler.ResponderAsync(1, Enviada("KK", "ALL-IN", ms: 800), Hoy, default);

        Assert.True(Assert.Single(bitacora.Filas).Acerto);
    }

    /// <summary>
    /// Una casilla que ya no existe no se registra: no hubo respuesta que
    /// contar, y ensuciar la bitácora con eso torcería cualquier estadística
    /// que salga de ella.
    /// </summary>
    [Fact]
    public async Task Una_casilla_que_ya_no_existe_no_deja_registro()
    {
        var bitacora = new BitacoraEnMemoria();
        var (handler, _) = Armar(bitacora);

        var v = await handler.ResponderAsync(
            1, new RespuestaEnviada("NO_EXISTE", "9-11bb", "SB_OR", "KK", "FOLD"), Hoy, default);

        Assert.Null(v);
        Assert.Empty(bitacora.Filas);
    }

    /// <summary>
    /// Sin tiempo medido se guarda cero, no un valor inventado: contarlo como
    /// rápido sería peor que no contarlo. Y el acierto vale igual — perderlo
    /// por no haber medido sería el peor intercambio posible.
    /// </summary>
    [Fact]
    public async Task Sin_tiempo_medido_se_guarda_cero_y_la_respuesta_vale_igual()
    {
        var bitacora = new BitacoraEnMemoria();
        var (handler, _) = Armar(bitacora);

        var v = await handler.ResponderAsync(1, Enviada("KK", "ALL-IN"), Hoy, default);

        Assert.True(v!.Acerto);
        Assert.Equal(0, Assert.Single(bitacora.Filas).Milisegundos);
    }

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
