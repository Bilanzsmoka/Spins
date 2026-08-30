using Microsoft.EntityFrameworkCore;
using PokerProOS.Api.Voz;
using PokerProOS.Application.Glosario;
using PokerProOS.Infrastructure.Glosario;
using PokerProOS.Infrastructure.Plan;
using PokerProOS.Application.Bitacora;
using PokerProOS.Application.Diario;
using PokerProOS.Application.Entrenador;
using PokerProOS.Application.Tablas;
using PokerProOS.Application.Voz;
using PokerProOS.Infrastructure;
using PokerProOS.Infrastructure.Database;
using PokerProOS.Infrastructure.Entrenador;
using PokerProOS.Infrastructure.Tablas;
using PokerProOS.Infrastructure.Diario;
using PokerProOS.Infrastructure.Voz;

var builder = WebApplication.CreateBuilder(args);

// La app no solo LEE database/: también lo escribe (corregir una celda, un tip,
// enseñarle una forma nueva de decir algo). Apuntando a la copia de la salida,
// todo eso se guardaba en bin/ — fuera de git, invisible para el usuario, y a un
// `git clean` de perderse. Se enseñaron doce formas así antes de descubrirlo.
//
// Corriendo desde el repo se usa el database/ del repo, que es la fuente de
// verdad que documenta CLAUDE.md. Publicada, no hay repo: ahí sí vale la copia
// que el csproj dejó junto al ejecutable.
var carpetaDatos = CarpetaDeDatos();

static string CarpetaDeDatos()
{
    var actual = new DirectoryInfo(AppContext.BaseDirectory);
    while (actual is not null)
    {
        // Las dos marcas juntas: bin/ también tiene un database/, y sin la
        // solución al lado se lo confundiría con la raíz del repositorio.
        if (File.Exists(Path.Combine(actual.FullName, "PokerProOS.slnx"))
            && Directory.Exists(Path.Combine(actual.FullName, "database")))
            return Path.Combine(actual.FullName, "database");
        actual = actual.Parent;
    }
    return Path.Combine(AppContext.BaseDirectory, "database");
}

// acciones.json y vocabulario.json son el único dato que el usuario edita a
// mano (el caso de uso central del proyecto). Si el archivo falta o tiene
// un error de sintaxis, no hay nada útil que servir: colores, validación de
// tablas y la interpretación de lo dictado dependen del registro. A esta altura no hay
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

// El glosario NO usa CargarRegistroOTerminar: es material de estudio, no
// configuracion. Si falta, la app arranca igual y la pantalla queda vacia —
// una app que no abre por un diccionario seria peor que una sin diccionario.
builder.Services.AddSingleton(RegistroDeGlosarioJson.Cargar(
    Path.Combine(carpetaDatos, "registro", "glosario.json")));
// El plan tampoco tumba el arranque: sin el archivo no hay panel del día, y
// las tablas y la voz siguen sirviéndose igual.
builder.Services.AddSingleton(RegistroDelPlanJson.Cargar(
    Path.Combine(carpetaDatos, "registro", "plan.json")));
builder.Services.AddSingleton(habitos);
builder.Services.AddSingleton<ICatalogoDeTablas>(catalogo);
builder.Services.AddSingleton<IEditorDeTablas>(
    new EditorDeTablasJson(carpetaDeTablas, catalogo, cargador));
builder.Services.AddSingleton<IEditorDeVocabulario>(
    new EditorDeVocabularioJson(rutaDeVocabulario, vocabularioVivo));
builder.Services.AddSingleton<ResolverManoHandler>();
builder.Services.AddSingleton<RedactorDeRespuesta>();
builder.Services.AddSingleton<AnalizadorDeMemoria>();
builder.Services.AddSingleton<PlanificadorDeTanda>();
builder.Services.AddSingleton<InterpretadorDeRespuesta>();
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
// Scoped como el resto de lo que toca la base: el DbContext lo es.
builder.Services.AddScoped<IProgresoDeEntrenamiento, ProgresoDeEntrenamientoSql>();
builder.Services.AddScoped<ArmarTandaHandler>();
builder.Services.AddScoped<ResponderRespuestaHandler>();

// Los enums salen como palabra, no como numero: la pantalla compara el tipo
// de dictado contra 'Contexto' e 'Ignorado'. Es la misma configuracion que
// usa el SSE, definida una sola vez en JsonDeLaApi.
builder.Services.AddControllers()
    .AddJsonOptions(opciones => JsonDeLaApi.Aplicar(opciones.JsonSerializerOptions));
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
//
// Las migraciones si esperan: son baratas cuando ya estan aplicadas, y el
// entrenador necesita su tabla antes de la primera consulta.
using (var alcance = app.Services.CreateScope())
{
    try
    {
        var contexto = alcance.ServiceProvider.GetRequiredService<PokerProOSDbContext>();
        await contexto.Database.MigrateAsync();
    }
    catch (Exception ex)
    {
        app.Logger.LogWarning(ex,
            "Sin base de datos. Las tablas funcionan igual, pero no hay historial de consultas.");
    }
}

// El espejo relacional NO espera. Son 339 spots por 169 manos: 57.291 filas
// que se borran y se reescriben enteras en cada arranque, y con eso el
// SaveChanges tardaba 35 segundos y moria por timeout — o sea que la app
// tardaba mas de medio minuto en levantar para no terminar de escribir una
// tabla que, hoy, no lee nadie. Los JSON son la fuente de verdad y el
// catalogo vive en memoria: nada de lo que se estudia depende de esto.
//
// Va en fuego y olvido, igual que la bitacora. Si la base no esta o tarda,
// la app ya esta sirviendo tablas.
_ = SincronizarElEspejoAsync();

async Task SincronizarElEspejoAsync()
{
    try
    {
        using var alcance = fabricaDeAlcances.CreateScope();
        var contexto = alcance.ServiceProvider.GetRequiredService<PokerProOSDbContext>();
        var filas = await new SincronizadorDeCatalogo(contexto).SincronizarAsync(catalogo, default);
        app.Logger.LogInformation("Catálogo sincronizado: {Filas} celdas.", filas);
    }
    catch (Exception ex)
    {
        app.Logger.LogWarning(ex, "No se pudo sincronizar el espejo del catálogo.");
    }
}

app.UseMiddleware<PokerProOS.Api.Middleware.ExceptionMiddleware>();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.MapControllers();
// index.html NUNCA se cachea; los bundles con hash, para siempre.
//
// Cada build borra wwwroot y Vite escribe un index-<hash>.js nuevo. Servido
// solo con ETag —sin Cache-Control—, el navegador cachea index.html por
// heuristica y despues pide un bundle que ya no existe: la app no arranca y
// no dice por que. Eso es lo que hacia falta un Ctrl+Shift+R despues de cada
// build, y lo que hacia parecer que se rompian cosas al azar.
//
// Los assets llevan el hash del contenido en el nombre, asi que son inmutables
// por construccion: si cambia el contenido, cambia la URL. Esos si se cachean
// un año.
var opcionesDeEstaticos = new StaticFileOptions
{
    OnPrepareResponse = contexto =>
    {
        var esElIndice = contexto.File.Name.Equals("index.html", StringComparison.OrdinalIgnoreCase);
        contexto.Context.Response.Headers.CacheControl = esElIndice
            ? "no-cache, no-store, must-revalidate"
            : "public, max-age=31536000, immutable";
    }
};

app.UseDefaultFiles();
app.UseStaticFiles(opcionesDeEstaticos);
app.MapFallbackToFile("index.html", opcionesDeEstaticos);

app.Run();
