using System.Text.Json;
using Microsoft.AspNetCore.Mvc;
using PokerProOS.Api.Voz;
using PokerProOS.Application.Voz;

namespace PokerProOS.Api.Controllers;

public record DichoEnviado(string Dicho);

public record ContextoEnviado(string Situacion, decimal StackBB, string Spot);

public record DictadoEnviado(string? Texto, float Confianza = 0.9f);

[ApiController]
[Route("api/voz")]
public sealed class VozController(
    CanalDeEventos canal,
    IRegistroDeVocabulario vocabulario,
    IEditorDeVocabulario editor,
    MemoriaDeContexto memoria,
    InterpretadorDeTexto interprete,
    CopilotoDeVoz copilotoDeVoz) : ControllerBase
{
    /// <summary>
    /// La tabla que la pantalla tiene abierta. Sin esto la pantalla y la voz
    /// llevan dos contextos separados: al dictar una mano el copiloto la
    /// resuelve contra el suyo —el de arranque o el del último dictado— y el
    /// evento publicado arrastra la pantalla hasta ahí, sacando al usuario de
    /// la tabla que estaba mirando.
    /// </summary>
    [HttpPut("contexto")]
    public IActionResult Contexto([FromBody] ContextoEnviado enviado)
    {
        memoria.Situacion = enviado.Situacion;
        memoria.StackBB = enviado.StackBB;
        memoria.Spot = enviado.Spot;
        return Ok(new { memoria.Situacion, memoria.StackBB, memoria.Spot });
    }

    /// <summary>Todo el vocabulario, para el módulo de configuración.</summary>
    [HttpGet("vocabulario")]
    public IActionResult Vocabulario() => Ok(new
    {
        palabrasDeStack = vocabulario.PalabrasDeStack,
        rangos = vocabulario.Rangos,
        palos = vocabulario.Palos,
        spots = vocabulario.Spots,
        situaciones = vocabulario.Situaciones,
    });

    [HttpPost("vocabulario/{categoria}/{clave}")]
    public async Task<IActionResult> Agregar(
        CategoriaDeVocabulario categoria, string clave,
        [FromBody] DichoEnviado enviado, CancellationToken ct)
    {
        var resultado = await editor.AgregarAsync(categoria, clave, enviado.Dicho, ct);
        return resultado.Exito ? Ok() : BadRequest(new { error = resultado.Error });
    }

    [HttpDelete("vocabulario/{categoria}/{clave}")]
    public async Task<IActionResult> Quitar(
        CategoriaDeVocabulario categoria, string clave,
        [FromQuery] string dicho, CancellationToken ct)
    {
        var resultado = await editor.QuitarAsync(categoria, clave, dicho, ct);
        return resultado.Exito ? Ok() : BadRequest(new { error = resultado.Error });
    }

    // JsonSerializer.Serialize sin opciones no aplica la politica camelCase
    // que si usan los controladores via Ok(...); sin esto el SSE manda las
    // propiedades en PascalCase y el front (que espera camelCase, igual que
    // el resto de la API) no puede leer el evento.
    private static readonly JsonSerializerOptions OpcionesJson = new(JsonSerializerDefaults.Web);

    /// <summary>
    /// Quién escucha y si está encendido lo sabe el navegador, no el servidor:
    /// acá solo queda la última frase que llegó, que sirve para saber si el
    /// dictado está entrando.
    /// </summary>
    [HttpGet("estado")]
    public IActionResult Estado() => Ok(new { ultimaFrase = canal.Ultimo?.TextoCrudo });

    /// <summary>
    /// El texto que oyó el navegador. Un texto que el intérprete rechaza no es un
    /// error: es conversación que no era para la app. Devolver 400 llenaría la
    /// consola de rojo por hablar cerca del micrófono.
    /// </summary>
    [HttpPost("dictado")]
    public IActionResult Dictado([FromBody] DictadoEnviado enviado)
    {
        var texto = enviado.Texto ?? "";
        var dictado = interprete.Interpretar(texto, enviado.Confianza);
        if (dictado is null)
        {
            // Descartado no es invisible. El camino viejo decía "No te
            // entendí" y lo mostraba; al mudar la voz al navegador eso se
            // perdió, y con él la única forma de saber QUÉ oyó el micrófono
            // cuando algo no anda. Sin esto, depurar el reconocimiento es
            // adivinar.
            //
            // La respuesta va vacía a propósito: el navegador no habla lo que
            // no tiene texto, así que la frase aparece en el historial sin
            // cantar "no te entendí" cada vez que alguien conversa al lado.
            canal.Publicar(new EventoDeCopiloto(texto, "", "", "", false, null, null, null));
            return Ok(new { ignorado = true });
        }

        return Ok(copilotoDeVoz.Procesar(dictado));
    }

    [HttpGet("eventos")]
    public async Task Eventos(CancellationToken cancelacion)
    {
        Response.Headers.ContentType = "text/event-stream";
        Response.Headers.CacheControl = "no-cache";

        var (lector, suscripcion) = canal.Suscribir();
        using (suscripcion)
        {
            try
            {
                await foreach (var evento in lector.ReadAllAsync(cancelacion))
                {
                    await Response.WriteAsync($"data: {JsonSerializer.Serialize(evento, OpcionesJson)}\n\n", cancelacion);
                    await Response.Body.FlushAsync(cancelacion);
                }
            }
            catch (OperationCanceledException)
            {
                // El navegador cerró la conexión. Es lo normal al recargar.
            }
        }
    }
}
