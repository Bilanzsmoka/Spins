using System.Text.Json;
using Microsoft.AspNetCore.Mvc;
using PokerProOS.Api.Controllers;
using PokerProOS.Api.Voz;
using PokerProOS.Application.Tablas;
using PokerProOS.Application.Voz;
using PokerProOS.Infrastructure.Tablas;
using PokerProOS.Infrastructure.Voz;

namespace PokerProOS.Tests.Voz;

/// <summary>
/// El endpoint entero, con el intérprete real y un copiloto armado sobre el
/// catálogo de verdad. InterpretarYResolverTests cubre las dos piezas por
/// separado; lo que solo se ve acá es el contrato con el navegador: qué código
/// HTTP y qué cuerpo sale de cada caso.
/// </summary>
public class EndpointDeDictadoTests
{
    private static VozController Armar()
    {
        var acciones = RegistroDeAccionesJson.Cargar(Rutas.Registro("acciones.json"));
        var vocabulario = RegistroDeVocabularioJson.Cargar(Rutas.Registro("vocabulario.json"));
        var catalogo = new CargadorDeTablas(new ValidadorDeTabla(acciones), acciones)
            .CargarDirectorio(Rutas.SemillasDeTablas);
        var memoria = new MemoriaDeContexto
        {
            Situacion = "HU_SB_OR_FISH", StackBB = 7, Spot = "SB_OR",
        };

        return new VozController(
            new CanalDeEventos(),
            vocabulario,
            new EditorMudo(),
            memoria,
            new InterpretadorDeTexto(vocabulario),
            new CopilotoDeVoz(
                new ResolverManoHandler(catalogo),
                new RedactorDeRespuesta(acciones, vocabulario),
                memoria,
                new AnalizadorDeMemoria(catalogo),
                catalogo));
    }

    [Fact]
    public void Un_texto_que_resuelve_devuelve_el_evento()
    {
        var resultado = Armar().Dictado(new DictadoEnviado("as as"));

        var ok = Assert.IsType<OkObjectResult>(resultado);
        var evento = Assert.IsType<EventoDeCopiloto>(ok.Value);
        Assert.True(evento.Resuelta);
        Assert.Equal("AA", evento.ManoInterpretada);
    }

    /// <summary>
    /// Hablar cerca del micrófono no es un error del usuario: el navegador
    /// manda todo lo que oye, y un 400 le pintaría la consola de rojo por
    /// conversar. Se contesta 200 con la frase marcada como ignorada.
    /// </summary>
    [Fact]
    public void Un_texto_que_no_es_una_orden_se_ignora_con_200()
        => AssertIgnorado(Armar().Dictado(new DictadoEnviado("contra el limite de gastos")));

    /// <summary>
    /// El navegador siempre manda un string, pero el contrato no puede depender
    /// de eso: un cuerpo sin la propiedad `texto` llega como null y tiene que
    /// caer en "no era una orden", no reventar en un 500.
    /// </summary>
    [Fact]
    public void Un_cuerpo_sin_texto_se_ignora_con_200()
        => AssertIgnorado(Armar().Dictado(new DictadoEnviado(null)));

    private static void AssertIgnorado(IActionResult resultado)
    {
        var ok = Assert.IsType<OkObjectResult>(resultado);
        // El controlador devuelve un anónimo; se compara por el JSON que sale,
        // que es lo que el navegador realmente lee.
        var cuerpo = JsonSerializer.SerializeToNode(
            ok.Value, new JsonSerializerOptions(JsonSerializerDefaults.Web))!;
        Assert.True(cuerpo["ignorado"]!.GetValue<bool>());
    }

    /// <summary>El endpoint de dictado no toca el editor; está solo para armarlo.</summary>
    private sealed class EditorMudo : IEditorDeVocabulario
    {
        public Task<ResultadoDeVocabulario> AgregarAsync(
            CategoriaDeVocabulario categoria, string clave, string dicho, CancellationToken ct)
            => throw new NotSupportedException();

        public Task<ResultadoDeVocabulario> QuitarAsync(
            CategoriaDeVocabulario categoria, string clave, string dicho, CancellationToken ct)
            => throw new NotSupportedException();
    }
}
