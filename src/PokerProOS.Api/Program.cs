using PokerProOS.Api.Voz;
using PokerProOS.Application.Tablas;
using PokerProOS.Application.Voz;
using PokerProOS.Infrastructure;
using PokerProOS.Infrastructure.Tablas;
using PokerProOS.Infrastructure.Voz;
using PokerProOS.Voz.Sapi;

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
var catalogo = new CargadorDeTablas(new ValidadorDeTabla(acciones))
    .CargarDirectorio(Path.Combine(carpetaDatos, "seed-data"));

builder.Services.AddSingleton(acciones);
builder.Services.AddSingleton(vocabulario);
builder.Services.AddSingleton(catalogo);
builder.Services.AddSingleton(new OpcionesDeVoz
{
    Cultura = builder.Configuration["Voz:Cultura"] ?? "es-ES",
    Voz = builder.Configuration["Voz:Voz"],
    ConfianzaMinima = builder.Configuration.GetValue("Voz:ConfianzaMinima", 0.35f)
});

builder.Services.AddSingleton<GeneradorDeGramatica>();
builder.Services.AddSingleton<IReconocedorDeVoz, ReconocedorSapi>();
builder.Services.AddSingleton<ISintetizadorDeVoz, SintetizadorSapi>();
builder.Services.AddSingleton<ResolverManoHandler>();
builder.Services.AddSingleton<RedactorDeRespuesta>();
builder.Services.AddSingleton(new MemoriaDeContexto
{
    Situacion = catalogo.Situaciones.FirstOrDefault()?.Clave ?? "",
    StackBB = 7,
    Spot = catalogo.Situaciones.FirstOrDefault()?.Stacks.FirstOrDefault()?.Spots.FirstOrDefault()?.Clave ?? ""
});
builder.Services.AddSingleton<CopilotoDeVoz>();
builder.Services.AddSingleton<CanalDeEventos>();
builder.Services.AddSingleton<ServicioDeCopiloto>();
builder.Services.AddHostedService(sp => sp.GetRequiredService<ServicioDeCopiloto>());

builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

var app = builder.Build();

foreach (var problema in catalogo.Problemas)
    app.Logger.LogWarning("Tabla inválida en {Archivo} ({Stack}/{Spot}): {Mensaje}",
        problema.Archivo, problema.Stack, problema.Spot, problema.Mensaje);

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
