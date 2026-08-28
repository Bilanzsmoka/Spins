using Microsoft.EntityFrameworkCore;
using PokerProOS.Api.Voz;
using PokerProOS.Application.Bitacora;
using PokerProOS.Application.Diario;
using PokerProOS.Application.Tablas;
using PokerProOS.Application.Voz;
using PokerProOS.Infrastructure;
using PokerProOS.Infrastructure.Database;
using PokerProOS.Infrastructure.Tablas;
using PokerProOS.Infrastructure.Diario;
using PokerProOS.Infrastructure.Voz;

var builder = WebApplication.CreateBuilder(args);

// Los datos viven junto al ejecutable: el csproj los copia a la salida.
// Nada de subir cinco directorios desde AppContext.BaseDirectory.
var carpetaDatos = Path.Combine(AppContext.BaseDirectory, "database");

// acciones.json y vocabulario.json son el único dato que el usuario edita a
// mano (el caso de uso central del proyecto). Si el archivo falta o tiene
// un error de sintaxis, no hay nada útil que servir: colores, validación de
// tablas y la gramática de voz dependen del registro. A esta altura no hay
// host ni logger todavía, así que el diagnóstico va a stderr y el proceso
// termina con código distinto de cero en vez de dejar escapar un stack
// trace en bruto.
static T CargarRegistroOTerminar<T>(Func<T> cargar)
{
    try
    {
        return cargar();
    }
    catch (RegistroInvalidoException ex)
    {
        Console.Error.WriteLine(ex.Message);
        Environment.Exit(1);
        throw; // Nunca se alcanza: Environment.Exit termina el proceso.
    }
}

var acciones = CargarRegistroOTerminar(() =>
    RegistroDeAccionesJson.Cargar(Path.Combine(carpetaDatos, "registro", "acciones.json")));
var vocabulario = CargarRegistroOTerminar(() =>
    RegistroDeVocabularioJson.Cargar(Path.Combine(carpetaDatos, "registro", "vocabulario.json")));
// Vivo: el editor de vocabulario lo reemplaza y el interprete lo relee.
var habitos = CargarRegistroOTerminar(() =>
    RegistroDeHabitosJson.Cargar(Path.Combine(carpetaDatos, "registro", "habitos.json")));
var rutaDeVocabulario = Path.Combine(carpetaDatos, "registro", "vocabulario.json");
var vocabularioVivo = new VocabularioVivo(vocabulario);
var carpetaDeTablas = Path.Combine(carpetaDatos, "seed-data");
var cargador = new CargadorDeTablas(new ValidadorDeTabla(acciones), acciones);
// Vivo: el editor reescribe el JSON y reemplaza el catálogo sin reiniciar.
var catalogo = new CatalogoVivo(cargador.CargarDirectorio(carpetaDeTablas));

builder.Services.AddSingleton(acciones);
builder.Services.AddSingleton<IRegistroDeVocabulario>(vocabularioVivo);
builder.Services.AddSingleton(habitos);
builder.Services.AddSingleton<ICatalogoDeTablas>(catalogo);
builder.Services.AddSingleton<IEditorDeTablas>(
    new EditorDeTablasJson(carpetaDeTablas, catalogo, cargador));
builder.Services.AddSingleton<IEditorDeVocabulario>(
    new EditorDeVocabularioJson(rutaDeVocabulario, vocabularioVivo));
builder.Services.AddSingleton<ResolverManoHandler>();
builder.Services.AddSingleton<RedactorDeRespuesta>();
builder.Services.AddSingleton<AnalizadorDeMemoria>();
builder.Services.AddSingleton(new MemoriaDeContexto
{
    Situacion = catalogo.Situaciones.FirstOrDefault()?.Clave ?? "",
    StackBB = 7,
    Spot = catalogo.Situaciones.FirstOrDefault()?.Stacks.FirstOrDefault()?.Spots.FirstOrDefault()?.Clave ?? ""
});
builder.Services.AddSingleton<CopilotoDeVoz>();
builder.Services.AddSingleton<InterpretadorDeTexto>();
builder.Services.AddSingleton<CanalDeEventos>();

builder.Services.AddDbContext<PokerProOSDbContext>(opciones =>
    opciones.UseSqlServer(builder.Configuration.GetConnectionString("DefaultConnection")));
builder.Services.AddScoped<IBitacoraDeConsultas, BitacoraDeConsultas>();
builder.Services.AddScoped<IRepositorioDeDiario, RepositorioDeDiario>();

builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

var app = builder.Build();

// Sin un servicio de fondo que lo conecte, el copiloto se engancha acá: ahora
// el dictado entra por HTTP y lo único que él hace es levantar el evento. Los
// dos destinos son el SSE que resalta la celda y la bitácora.
var copiloto = app.Services.GetRequiredService<CopilotoDeVoz>();
var canalDeEventos = app.Services.GetRequiredService<CanalDeEventos>();
var fabricaDeAlcances = app.Services.GetRequiredService<IServiceScopeFactory>();

copiloto.Publicado += (_, evento) =>
{
    canalDeEventos.Publicar(evento);
    // La bitácora es Scoped y este callback no tiene alcance propio. Va en
    // fuego y olvido a propósito: una base caída no puede hacer esperar la
    // respuesta que el usuario está por oír.
    _ = RegistrarEnBitacoraAsync(evento);
};

async Task RegistrarEnBitacoraAsync(EventoDeCopiloto evento)
{
    try
    {
        using var alcance = fabricaDeAlcances.CreateScope();
        var bitacora = alcance.ServiceProvider.GetRequiredService<IBitacoraDeConsultas>();
        await bitacora.RegistrarAsync(evento, default);
    }
    catch (Exception ex)
    {
        app.Logger.LogWarning(ex, "No se pudo registrar la consulta en la bitácora.");
    }
}

foreach (var problema in catalogo.Problemas)
    app.Logger.LogWarning("Tabla inválida en {Archivo} ({Stack}/{Spot}): {Mensaje}",
        problema.Archivo, problema.Stack, problema.Spot, problema.Mensaje);

// La base es opcional: si no esta, se estudia igual sin historial.
using (var alcance = app.Services.CreateScope())
{
    try
    {
        var contexto = alcance.ServiceProvider.GetRequiredService<PokerProOSDbContext>();
        await contexto.Database.MigrateAsync();
        var filas = await new SincronizadorDeCatalogo(contexto).SincronizarAsync(catalogo, default);
        app.Logger.LogInformation("Catálogo sincronizado: {Filas} celdas.", filas);
    }
    catch (Exception ex)
    {
        app.Logger.LogWarning(ex,
            "Sin base de datos. Las tablas funcionan igual, pero no hay historial de consultas.");
    }
}

app.UseMiddleware<PokerProOS.Api.Middleware.ExceptionMiddleware>();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.MapControllers();
app.UseDefaultFiles();
app.UseStaticFiles();
app.MapFallbackToFile("index.html");

app.Run();
