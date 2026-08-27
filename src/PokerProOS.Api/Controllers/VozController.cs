using System.Text.Json;
using Microsoft.AspNetCore.Mvc;
using PokerProOS.Api.Voz;
using PokerProOS.Application.Voz;

namespace PokerProOS.Api.Controllers;

public record DichoEnviado(string Dicho);

public record ContextoEnviado(string Situacion, decimal StackBB, string Spot);

[ApiController]
[Route("api/voz")]
public sealed class VozController(
    CanalDeEventos canal,
    ServicioDeCopiloto copiloto,
    IRegistroDeVocabulario vocabulario,
    IEditorDeVocabulario editor,
    IReconocedorDeVoz reconocedor,
    MemoriaDeContexto memoria) : ControllerBase
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

    /// <summary>
    /// Escucha una vez con dictado libre y devuelve lo que entendió, tal cual.
    /// No busca acertar: busca capturar cómo suena esta persona diciendo eso,
    /// para agregarlo como forma válida.
    /// </summary>
    [HttpPost("capturar")]
    public IActionResult Capturar([FromQuery] int segundos = 6)
    {
        if (!copiloto.Escuchando)
            return StatusCode(503, new { error = copiloto.Falla ?? "El motor de voz no está disponible." });

        var texto = reconocedor.CapturarDictadoLibre(TimeSpan.FromSeconds(Math.Clamp(segundos, 2, 15)));
        return Ok(new { texto });
    }

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

    [HttpGet("estado")]
    public IActionResult Estado() => Ok(new
    {
        escuchando = copiloto.Escuchando,
        activo = copiloto.Activo,
        falla = copiloto.Falla,
        fallaAlHablar = copiloto.FallaAlHablar,
        ultimaFrase = canal.Ultimo?.TextoCrudo
    });

    /// <summary>
    /// Enciende la escucha. Se usa al empezar una sesión de juego.
    /// </summary>
    [HttpPost("encender")]
    public IActionResult Encender() => copiloto.Encender()
        ? Ok(new { activo = true })
        : StatusCode(503, new { error = copiloto.Falla ?? "El motor de voz no está disponible." });

    /// <summary>
    /// Apaga la escucha. Se usa al terminar de jugar, para que la aplicación
    /// no conteste sola mientras no está en sesión.
    /// </summary>
    [HttpPost("apagar")]
    public IActionResult Apagar() => copiloto.Apagar()
        ? Ok(new { activo = false })
        : StatusCode(503, new { error = copiloto.Falla ?? "El motor de voz no está disponible." });

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
