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
    IProgresoDeEntrenamiento progreso)
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

        var acerto = Acierta(correcta, respuesta.Accion);

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

        var calculado = CalendarioDeRepeticion.Siguiente(fila.AciertosSeguidos, acerto, hoy);
        fila.AciertosSeguidos = calculado.AciertosSeguidos;
        fila.IntervaloEnDias = calculado.IntervaloEnDias;
        fila.Vence = calculado.Vence;
        await progreso.GuardarAsync(fila, ct);

        // La ficha solo al fallar: acertar sigue de largo, y es al errar
        // cuando una explicacion entra de verdad.
        var ficha = acerto
            ? null
            : analizador.Analizar(
                respuesta.Situacion, respuesta.ClaveDeStack, respuesta.Spot, respuesta.Mano);

        return new VeredictoDeRespuesta(
            acerto, correcta.Accion, correcta.Mix, ficha, calculado.Vence);
    }

    /// <summary>
    /// Una mano mixta cuenta por cualquiera de sus partes: elegir una como "la
    /// correcta" sería inventar una estrategia que la tabla no declara.
    /// </summary>
    private static bool Acierta(RespuestaDeMano correcta, string elegida)
    {
        if (correcta.Mix is { Count: > 1 } partes)
            return partes.Any(p =>
                string.Equals(p.Accion, elegida, StringComparison.OrdinalIgnoreCase));

        return string.Equals(correcta.Accion, elegida, StringComparison.OrdinalIgnoreCase);
    }
}
