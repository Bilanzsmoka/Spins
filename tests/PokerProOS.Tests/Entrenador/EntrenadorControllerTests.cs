using System.Data.Common;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging.Abstractions;
using PokerProOS.Api.Controllers;
using PokerProOS.Application.Entrenador;
using PokerProOS.Application.Tablas;
using PokerProOS.Domain.Entrenador;
using PokerProOS.Domain.Manos;
using PokerProOS.Domain.Tablas;
using PokerProOS.Infrastructure.Tablas;

namespace PokerProOS.Tests.Entrenador;

public class EntrenadorControllerTests
{
    private sealed class ProgresoEnMemoria : IProgresoDeEntrenamiento
    {
        public List<ProgresoDeCasilla> Filas { get; } = [];

        public Task<IReadOnlyList<ProgresoDeCasilla>> VencidasAsync(
            int usuarioId, DateOnly hoy, CancellationToken ct)
            => Task.FromResult<IReadOnlyList<ProgresoDeCasilla>>(
                Filas.Where(f => f.Vence <= hoy).ToList());

        public Task<IReadOnlyList<ProgresoDeCasilla>> TodasAsync(int usuarioId, CancellationToken ct)
            => Task.FromResult<IReadOnlyList<ProgresoDeCasilla>>(Filas);

        public Task<ProgresoDeCasilla?> BuscarAsync(
            int usuarioId, string situacion, string claveDeStack, string spot, string mano,
            CancellationToken ct)
            => Task.FromResult(Filas.FirstOrDefault(f => f.Mano == mano && f.Spot == spot));

        public Task GuardarAsync(ProgresoDeCasilla progreso, CancellationToken ct)
        {
            if (!Filas.Contains(progreso)) Filas.Add(progreso);
            return Task.CompletedTask;
        }
    }

    /// <summary>
    /// Una base caída. DbException es abstracta y no se puede instanciar: se
    /// hereda para poder probar el borde sin levantar un SQL Server.
    /// </summary>
    private sealed class BaseCaida : DbException
    {
        public BaseCaida() : base("No se pudo abrir la conexión.") { }
    }

    /// <summary>El progreso cuando la base no responde: todo tira.</summary>
    private sealed class ProgresoQueFalla : IProgresoDeEntrenamiento
    {
        public Task<IReadOnlyList<ProgresoDeCasilla>> VencidasAsync(
            int usuarioId, DateOnly hoy, CancellationToken ct) => throw new BaseCaida();

        public Task<IReadOnlyList<ProgresoDeCasilla>> TodasAsync(int usuarioId, CancellationToken ct)
            => throw new BaseCaida();

        public Task<ProgresoDeCasilla?> BuscarAsync(
            int usuarioId, string situacion, string claveDeStack, string spot, string mano,
            CancellationToken ct) => throw new BaseCaida();

        public Task GuardarAsync(ProgresoDeCasilla progreso, CancellationToken ct)
            => throw new BaseCaida();
    }

    /// <summary>
    /// La bitácora no participa de lo que prueba esta clase. Cuando el caso es
    /// "la base se cayó", el progreso revienta antes de llegar acá — que es
    /// justo el orden que importa: lo que no puede perderse es la graduación
    /// de la casilla.
    /// </summary>
    private sealed class BitacoraNula : IBitacoraDeRespuestas
    {
        public Task RegistrarAsync(RespuestaRegistrada respuesta, CancellationToken ct)
            => Task.CompletedTask;

        public Task<IReadOnlyList<ErrorRepetido>> ErroresRepetidosAsync(
            int usuarioId, int cuantos, CancellationToken ct)
            => Task.FromResult<IReadOnlyList<ErrorRepetido>>([]);
    }

    private static EntrenadorController Armar()
        => Armar(new ProgresoEnMemoria());

    private static EntrenadorController Armar(IProgresoDeEntrenamiento progreso)
    {
        var acciones = RegistroDeAccionesJson.Cargar(Rutas.Registro("acciones.json"));
        var catalogo = new CargadorDeTablas(new ValidadorDeTabla(acciones), acciones)
            .CargarDirectorio(Rutas.SemillasDeTablas);

        return new EntrenadorController(
            new ArmarTandaHandler(progreso, new PlanificadorDeTanda(catalogo)),
            new ResponderRespuestaHandler(
                new ResolverManoHandler(catalogo),
                new AnalizadorDeMemoria(catalogo),
                catalogo,
                progreso,
                new BitacoraNula(),
                acciones),
            catalogo,
            acciones,
            new InterpretadorDeRespuesta(acciones),
            new BitacoraNula(),
            NullLogger<EntrenadorController>.Instance);
    }

    // IsAssignableFrom y no IsType: T es una interfaz (IReadOnlyList<...>) y el
    // valor real que devuelve Ok() es la lista concreta que arma el handler —
    // IsType exige coincidencia exacta de tipo en tiempo de ejecución, que
    // ninguna interfaz puede cumplir nunca.
    private static T Cuerpo<T>(IActionResult resultado)
        => Assert.IsAssignableFrom<T>(Assert.IsType<OkObjectResult>(resultado).Value);

    [Fact]
    public async Task La_tanda_devuelve_el_tamano_pedido()
    {
        var preguntas = Cuerpo<IReadOnlyList<PreguntaDeTanda>>(
            await Armar().Tanda(new TandaPedida(null, null, null, null, null, 5), default));

        Assert.Equal(5, preguntas.Count);
    }

    /// <summary>
    /// Un tamaño absurdo no puede hacer que el servidor arme una tanda de un
    /// millón de preguntas: se recorta antes de planificar.
    /// </summary>
    [Fact]
    public async Task Un_tamano_fuera_de_rango_se_recorta()
    {
        var preguntas = Cuerpo<IReadOnlyList<PreguntaDeTanda>>(
            await Armar().Tanda(new TandaPedida(null, null, null, null, null, 5000), default));

        Assert.Equal(EntrenadorController.TamanoMaximo, preguntas.Count);
    }

    [Fact]
    public async Task Responder_una_casilla_inexistente_da_404()
    {
        var resultado = await Armar().Responder(
            new RespuestaEnviada("NO_EXISTE", "1-5bb", "SB_OR", "AA", "FOLD"), default);

        Assert.IsType<NotFoundObjectResult>(resultado);
    }

    /// <summary>
    /// Los botones salen del spot, no de una lista en código, y traen el color
    /// y el orden del registro: es la misma memoria visual que el usuario ya
    /// entrenó mirando las grillas.
    /// </summary>
    [Fact]
    public void Las_acciones_de_un_spot_salen_del_spot_con_su_color()
    {
        var acciones = Cuerpo<IReadOnlyList<AccionDefinida>>(
            Armar().Acciones("HU_SB_OR_FISH", "1-4bb", "SB_OR"));

        Assert.NotEmpty(acciones);
        Assert.All(acciones, a => Assert.StartsWith("#", a.Color));
        Assert.Equal(acciones.OrderBy(a => a.Orden), acciones);
    }

    [Fact]
    public void Las_acciones_de_un_spot_inexistente_dan_404()
        => Assert.IsType<NotFoundObjectResult>(
            Armar().Acciones("HU_SB_OR_FISH", "1-4bb", "NO_EXISTE"));

    /// <summary>
    /// El entrenador es lo único que no anda sin base, y el usuario tiene que
    /// enterarse de eso: el middleware genérico contestaba
    /// «An internal error occurred», en inglés y sin nombrar la base.
    /// </summary>
    [Fact]
    public async Task Sin_base_la_tanda_lo_dice_en_castellano()
    {
        var resultado = await Armar(new ProgresoQueFalla())
            .Tanda(new TandaPedida(null, null, null, null, null, 5), default);

        var respuesta = Assert.IsType<ObjectResult>(resultado);
        Assert.Equal(StatusCodes.Status503ServiceUnavailable, respuesta.StatusCode);
        Assert.Contains("base de datos", respuesta.Value!.ToString());
    }

    [Fact]
    public async Task Sin_base_la_respuesta_avisa_que_no_quedo_guardada()
    {
        var resultado = await Armar(new ProgresoQueFalla()).Responder(
            new RespuestaEnviada("HU_SB_OR_FISH", "1-4bb", "SB_OR", "AA", "ALL-IN"), default);

        var respuesta = Assert.IsType<ObjectResult>(resultado);
        Assert.Equal(StatusCodes.Status503ServiceUnavailable, respuesta.StatusCode);
    }
}
