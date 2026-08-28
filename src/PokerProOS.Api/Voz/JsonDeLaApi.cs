using System.Text.Json;
using System.Text.Json.Serialization;

namespace PokerProOS.Api.Voz;

/// <summary>
/// Cómo se serializa todo lo que sale de la API, en un solo lugar.
///
/// Existe por un enum. <c>TipoDeDictado</c> viajaba como número —0, 1, 2— y la
/// pantalla lo compara contra las palabras 'Mano', 'Contexto' e 'Ignorado':
/// ninguna comparación era cierta nunca, así que una orden de contexto que se
/// había entendido perfecto se dibujaba como "No entendí". TypeScript no podía
/// avisarlo porque el JSON entra con un cast, sin validación en tiempo de
/// ejecución.
///
/// Los dos caminos de salida —los controladores vía <c>Ok(...)</c> y el
/// <c>JsonSerializer.Serialize</c> a mano del SSE— tienen que configurarse
/// igual o el mismo evento sale distinto según por dónde salga; por eso
/// <see cref="Aplicar"/> es la única definición y los dos la llaman.
/// </summary>
public static class JsonDeLaApi
{
    public static JsonSerializerOptions Aplicar(JsonSerializerOptions opciones)
    {
        opciones.Converters.Add(new JsonStringEnumConverter());
        return opciones;
    }

    /// <summary>Para serializar a mano, fuera del pipeline de MVC.</summary>
    public static readonly JsonSerializerOptions Opciones =
        Aplicar(new JsonSerializerOptions(JsonSerializerDefaults.Web));
}
