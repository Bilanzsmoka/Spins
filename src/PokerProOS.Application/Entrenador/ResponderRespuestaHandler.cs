using PokerProOS.Application.Tablas;
using PokerProOS.Domain.Entrenador;
using PokerProOS.Domain.Manos;

namespace PokerProOS.Application.Entrenador;

/// <summary>
/// Resuelve la casilla, compara, mueve el calendario y arma la ficha al
/// fallar.
///
/// La respuesta correcta la resuelve <see cref="ResolverManoHandler"/>, el
/// mismo que contesta por voz: no hay una segunda fuente de verdad sobre qué
/// dice la tabla. Como ese handler razona en BB y en rangos sueltos, y el
/// entrenador tiene la clave de stack y la mano entera, se traduce acá —el
/// MinBB de la banda cae dentro de su cobertura por definición—.
/// </summary>
public sealed class ResponderRespuestaHandler(
    ResolverManoHandler resolver,
    AnalizadorDeMemoria analizador,
    ICatalogoDeTablas catalogo,
    IProgresoDeEntrenamiento progreso,
    IBitacoraDeRespuestas bitacora,
    IRegistroDeAcciones acciones)
{
    /// <summary>
    /// Null si esa casilla no existe en el catálogo. Pasa cuando una tabla se
    /// corrigió entre que se armó la tanda y se contestó: no es un error del
    /// usuario y no tiene que ensuciarle el progreso.
    /// </summary>
    public async Task<VeredictoDeRespuesta?> ResponderAsync(
        int usuarioId, RespuestaEnviada respuesta, DateOnly hoy, CancellationToken ct)
    {
        var tabla = catalogo.StackPorClave(respuesta.Situacion, respuesta.ClaveDeStack);
        if (tabla is null) return null;

        var (alto, bajo, palo) = MatrizDeManos.Partir(respuesta.Mano);
        var resultado = resolver.Resolver(new ConsultaDeMano(
            respuesta.Situacion, tabla.Stack.MinBB, respuesta.Spot, alto, bajo, palo));
        if (resultado.Respuesta is not { } correcta) return null;

        var calificacion = Calificar(correcta, respuesta.Accion);
        var acerto = calificacion == ResultadoDeRespuesta.Acierto;

        var fila = await progreso.BuscarAsync(
            usuarioId, respuesta.Situacion, respuesta.ClaveDeStack,
            respuesta.Spot, respuesta.Mano, ct)
            ?? new ProgresoDeCasilla
            {
                UsuarioId = usuarioId,
                Situacion = respuesta.Situacion,
                ClaveDeStack = respuesta.ClaveDeStack,
                Spot = respuesta.Spot,
                Mano = respuesta.Mano,
            };

        var calculado = CalendarioDeRepeticion.Siguiente(fila.AciertosSeguidos, calificacion, hoy);
        fila.AciertosSeguidos = calculado.AciertosSeguidos;
        fila.IntervaloEnDias = calculado.IntervaloEnDias;
        fila.Vence = calculado.Vence;
        await progreso.GuardarAsync(fila, ct);

        // El calendario guarda el estado y se pisa; esto guarda el hecho y no
        // se pisa nunca. Va después de graduar la casilla porque lo que no
        // puede perderse es el progreso: si algo falla acá, ya está anotado
        // que acertaste.
        await bitacora.RegistrarAsync(new RespuestaRegistrada
        {
            UsuarioId = usuarioId,
            Situacion = respuesta.Situacion,
            ClaveDeStack = respuesta.ClaveDeStack,
            Spot = respuesta.Spot,
            Mano = respuesta.Mano,
            AccionElegida = respuesta.Accion,
            AccionCorrecta = correcta.Accion,
            Acerto = acerto,
            Milisegundos = respuesta.Milisegundos,
        }, ct);

        // La ficha solo al fallar: acertar sigue de largo, y es al errar
        // cuando una explicacion entra de verdad.
        var ficha = acerto
            ? null
            : analizador.Analizar(
                respuesta.Situacion, respuesta.ClaveDeStack, respuesta.Spot, respuesta.Mano);

        return new VeredictoDeRespuesta(
            acerto, correcta.Accion, correcta.Mix, ficha, calculado.Vence,
            Cerca: calificacion == ResultadoDeRespuesta.Cerca);
    }

    /// <summary>
    /// Acierto, cerca o error. "Cerca" es una acción vecina en la escala de
    /// agresión del registro: erraste el tamaño, no el spot.
    ///
    /// La distancia se mide contra la acción correcta más parecida a la que
    /// elegiste, no contra la primera: en una mano mixta cualquiera de sus
    /// partes cuenta como acierto, así que la que está más cerca es la que
    /// define cuánto erraste.
    /// </summary>
    private ResultadoDeRespuesta Calificar(RespuestaDeMano correcta, string elegida)
    {
        var correctas = correcta.Mix is { Count: > 1 } partes
            ? partes.Select(p => p.Accion).ToList()
            : [correcta.Accion];

        if (correctas.Any(a => string.Equals(a, elegida, StringComparison.OrdinalIgnoreCase)))
            return ResultadoDeRespuesta.Acierto;

        var elegidaAgresion = Agresion(elegida);
        if (elegidaAgresion is null) return ResultadoDeRespuesta.Error;

        var distancia = correctas
            .Select(Agresion)
            .OfType<int>()
            .Select(a => Math.Abs(a - elegidaAgresion.Value))
            .DefaultIfEmpty(int.MaxValue)
            .Min();

        return distancia <= 1 ? ResultadoDeRespuesta.Cerca : ResultadoDeRespuesta.Error;
    }

    /// <summary>
    /// Null cuando la acción no está en el registro o no declara agresión. Sin
    /// escala no se puede decir que un error estuvo cerca, y ante la duda el
    /// error es error: sería peor perdonar de más.
    /// </summary>
    private int? Agresion(string clave)
    {
        if (!acciones.Existe(clave)) return null;
        var accion = acciones.Obtener(clave);
        return accion.Agresion > 0 ? accion.Agresion : null;
    }
}
