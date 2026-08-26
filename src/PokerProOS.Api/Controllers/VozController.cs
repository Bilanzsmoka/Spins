using System.Text.Json;
using Microsoft.AspNetCore.Mvc;
using PokerProOS.Api.Voz;

namespace PokerProOS.Api.Controllers;

[ApiController]
[Route("api/voz")]
public sealed class VozController(
    CanalDeEventos canal,
    ServicioDeCopiloto copiloto) : ControllerBase
{
    // JsonSerializer.Serialize sin opciones no aplica la politica camelCase
    // que si usan los controladores via Ok(...); sin esto el SSE manda las
    // propiedades en PascalCase y el front (que espera camelCase, igual que
    // el resto de la API) no puede leer el evento.
    private static readonly JsonSerializerOptions OpcionesJson = new(JsonSerializerDefaults.Web);

    [HttpGet("estado")]
    public IActionResult Estado() => Ok(new
    {
        escuchando = copiloto.Escuchando,
        falla = copiloto.Falla,
        fallaAlHablar = copiloto.FallaAlHablar,
        ultimaFrase = canal.Ultimo?.TextoCrudo
    });

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
