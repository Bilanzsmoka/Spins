# Tablas preflop y copiloto de voz — Plan de implementación

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Consultar las tablas preflop propias por voz, offline y sin micrófono de por medio para las pruebas, con la grilla resaltando la mano dictada.

**Architecture:** Los JSON en `database/` son la fuente de verdad. Al arrancar se validan y se cargan a un catálogo en memoria, que es el camino de lectura del copiloto de voz; en paralelo se sincronizan a SQL Server, que además guarda la bitácora de consultas. El reconocimiento usa `System.Speech` con gramática restringida generada del propio catálogo, aislado en un proyecto Windows detrás de una interfaz.

**Tech Stack:** .NET 10 (LTS), ASP.NET Core, EF Core 10 + SQL Server, `System.Speech` 10.0.11, React 19 + Vite 8 + TypeScript, xUnit.

**Spec:** [docs/superpowers/specs/2026-08-26-tablas-y-copiloto-de-voz-design.md](../specs/2026-08-26-tablas-y-copiloto-de-voz-design.md)

## Global Constraints

- **Nada hardcodeado.** Situaciones, stacks, spots y acciones se descubren de los datos. Prueba de fuego: agregar una tabla con una acción nueva debe funcionar de punta a punta sin tocar código.
- **Único valor constante permitido:** los 13 rangos de póker (`A K Q J T 9 8 7 6 5 4 3 2`) y el total de 169 manos.
- **Colores de acción exactos**, del proyecto original: `ALL-IN` = `#43bf55`, `CALL` = `#ffb743`, `RAISE_X2` = `#7c86dc`, `FOLD` = `#edf3fb`. Van en el registro, no en código.
- **Paleta base:** fondo `#0d1117`, panel `#151a21`, borde `#3a4350`, texto `#edf3fb`, apagado `#b0bac7`, acento `#8bb8e8`.
- **El color nunca es la única señal.** Cada celda lleva su etiqueta de mano visible.
- **Mano sin palo dictado resuelve a offsuit.** Las parejas no se ven afectadas. Cuando se aplica el default, la respuesta hablada repite la mano interpretada.
- **TFM:** `net10.0` para Domain, Application e Infrastructure. `net10.0-windows` para `PokerProOS.Voz.Sapi`, `PokerProOS.Api` y el proyecto de pruebas. Sin esto el compilador emite `CA1416` en cada llamada a SAPI.
- **Nombres de tipos y carpetas nuevos en español**, siguiendo el spec. El código preexistente en inglés se renombra solo cuando la tarea ya lo está tocando.
- **Cultura de voz:** `es-ES`. Reconocedor `MS-3082-80-DESK`, voces `Microsoft Helena Desktop`, `Microsoft Laura`, `Microsoft Pablo`.
- **La aplicación arranca aunque SQL Server no esté disponible.** El catálogo en memoria no depende de la base.
- **Un archivo de tabla inválido no aborta el arranque.** Se carga el resto y se marca el inválido.

## Hechos verificados antes de escribir este plan

No hace falta volver a comprobarlos:

- `System.Speech` versión `10.0.11` restaura sobre `net10.0`.
- El reconocedor español `MS-3082-80-DESK` y las voces `Helena`, `Laura` y `Pablo` (es-ES) están instalados.
- El mecanismo semántico de SAPI devuelve datos estructurados: `"siete be be a cinco offsuit"` produce `stack=7, alta=A, baja=5, palo=o`.
- Sintetizar a WAV y alimentar al reconocedor con `SetInputToWaveFile` funciona: **el bucle de voz se prueba sin micrófono**.
- La confianza sobre audio sintético queda entre 48% y 64%. El umbral debe ser configurable y bajo en pruebas.
- `Choices` **no** acepta `SemanticResultValue[]` en el constructor. Hay que instanciar vacío y usar `.Add()` en bucle.
- Hay que llamar a `motor.SetInputToNull()` antes de borrar un WAV que el reconocedor usó, o `File.Delete` falla con el archivo en uso.

## Estructura de archivos

| Archivo | Responsabilidad |
| --- | --- |
| `src/PokerProOS.Domain/Manos/MatrizDeManos.cs` | Genera las 169 manos y sus vecinas en la matriz |
| `src/PokerProOS.Domain/Tablas/CeldaDeTabla.cs` | Una mano con su acción dentro de un spot |
| `src/PokerProOS.Domain/Tablas/RangoDeStack.cs` | Clave de stack y su cobertura en BB |
| `src/PokerProOS.Application/Tablas/IRegistroDeAcciones.cs` | Contrato del registro de acciones |
| `src/PokerProOS.Application/Tablas/ICatalogoDeTablas.cs` | Contrato de lectura del catálogo |
| `src/PokerProOS.Application/Tablas/ResolverManoHandler.cs` | Mano + contexto a respuesta con borde |
| `src/PokerProOS.Application/Voz/IReconocedorDeVoz.cs` | Contrato del motor de reconocimiento |
| `src/PokerProOS.Application/Voz/ISintetizadorDeVoz.cs` | Contrato de la síntesis |
| `src/PokerProOS.Application/Voz/MemoriaDeContexto.cs` | Stack y spot activos entre consultas |
| `src/PokerProOS.Application/Voz/RedactorDeRespuesta.cs` | Arma la frase hablada |
| `src/PokerProOS.Infrastructure/Tablas/RegistroDeAccionesJson.cs` | Carga `acciones.json` |
| `src/PokerProOS.Infrastructure/Tablas/ValidadorDeTabla.cs` | Las seis reglas de validación |
| `src/PokerProOS.Infrastructure/Tablas/CargadorDeTablas.cs` | Lee los JSON y expande `REST` |
| `src/PokerProOS.Infrastructure/Tablas/CatalogoEnMemoria.cs` | Implementa `ICatalogoDeTablas` |
| `src/PokerProOS.Voz.Sapi/GeneradorDeGramatica.cs` | Construye la gramática del catálogo |
| `src/PokerProOS.Voz.Sapi/ReconocedorSapi.cs` | Reconocimiento con `System.Speech` |
| `src/PokerProOS.Voz.Sapi/SintetizadorSapi.cs` | Síntesis con voces locales |
| `src/PokerProOS.Api/Voz/CopilotoDeVozService.cs` | Bucle: escucha, resuelve, habla, publica |
| `src/PokerProOS.Api/Voz/CanalDeEventos.cs` | Cola de eventos para SSE |
| `database/registro/acciones.json` | Registro de acciones |
| `database/registro/vocabulario.json` | Formas habladas de rangos, palos y spots |
| `tests/PokerProOS.Tests/` | Todas las suites |

---

### Task 1: Matriz de manos y limpieza del andamiaje

**Files:**
- Create: `tests/PokerProOS.Tests/PokerProOS.Tests.csproj`
- Create: `tests/PokerProOS.Tests/Manos/MatrizDeManosTests.cs`
- Create: `src/PokerProOS.Domain/Manos/MatrizDeManos.cs`
- Delete: `src/PokerProOS.Api/WeatherForecast.cs`, `src/PokerProOS.Api/Controllers/WeatherForecastController.cs`, `src/PokerProOS.Domain/Enums/`, `src/PokerProOS.Domain/ValueObjects/HandLabel.cs`
- Modify: `PokerProOS.slnx`

**Interfaces:**
- Produces: `MatrizDeManos.Rangos` (`IReadOnlyList<char>`, 13 elementos, de A a 2), `MatrizDeManos.Todas()` (`IReadOnlyList<string>`, 169 etiquetas), `MatrizDeManos.Etiqueta(int fila, int columna)` (`string`), `MatrizDeManos.Vecinas(string etiqueta)` (`IReadOnlyList<string>`, entre 2 y 4 etiquetas adyacentes en la matriz).

La convención de la matriz, idéntica a la que ya usan el importador y la grilla del front: fila y columna recorren los rangos de A a 2; la diagonal son las parejas, arriba de la diagonal las suited, abajo las offsuit, y el rango más alto va siempre primero.

- [ ] **Step 1: Crear el proyecto de pruebas y engancharlo a la solución**

```bash
cd "c:/Users/BilanzSmoka/Pictures/Poker"
dotnet new xunit -o tests/PokerProOS.Tests --framework net10.0
dotnet add tests/PokerProOS.Tests reference src/PokerProOS.Domain
```

Editar `tests/PokerProOS.Tests/PokerProOS.Tests.csproj` y fijar `<TargetFramework>net10.0-windows</TargetFramework>`, porque más adelante estas pruebas van a instanciar SAPI.

Agregar el proyecto a `PokerProOS.slnx` dentro de una carpeta `/tests/`:

```xml
<Folder Name="/tests/">
  <Project Path="tests/PokerProOS.Tests/PokerProOS.Tests.csproj" />
</Folder>
```

- [ ] **Step 2: Escribir las pruebas que fallan**

```csharp
using PokerProOS.Domain.Manos;

namespace PokerProOS.Tests.Manos;

public class MatrizDeManosTests
{
    [Fact]
    public void Genera_exactamente_169_manos()
        => Assert.Equal(169, MatrizDeManos.Todas().Count);

    [Fact]
    public void No_repite_ninguna_mano()
    {
        var todas = MatrizDeManos.Todas();
        Assert.Equal(todas.Count, todas.Distinct().Count());
    }

    [Fact]
    public void Contiene_13_parejas()
        => Assert.Equal(13, MatrizDeManos.Todas().Count(m => m.Length == 2));

    [Theory]
    [InlineData(0, 0, "AA")]
    [InlineData(0, 1, "AKs")]
    [InlineData(1, 0, "AKo")]
    [InlineData(12, 12, "22")]
    [InlineData(4, 9, "T5s")]
    [InlineData(9, 4, "T5o")]
    public void Ubica_la_mano_en_la_celda_correcta(int fila, int columna, string esperada)
        => Assert.Equal(esperada, MatrizDeManos.Etiqueta(fila, columna));

    [Fact]
    public void Las_vecinas_de_una_celda_interior_son_cuatro()
    {
        var vecinas = MatrizDeManos.Vecinas("T5s");
        Assert.Equal(4, vecinas.Count);
        Assert.Contains("J5s", vecinas);
        Assert.Contains("95s", vecinas);
        Assert.Contains("T6s", vecinas);
        Assert.Contains("T4s", vecinas);
    }

    [Fact]
    public void Las_vecinas_de_una_esquina_son_dos()
        => Assert.Equal(2, MatrizDeManos.Vecinas("AA").Count);

    [Fact]
    public void Toda_mano_generada_tiene_vecinas_validas()
    {
        var todas = MatrizDeManos.Todas().ToHashSet();
        foreach (var mano in todas)
            Assert.All(MatrizDeManos.Vecinas(mano), v => Assert.Contains(v, todas));
    }
}
```

- [ ] **Step 3: Correr las pruebas y confirmar que fallan**

```bash
dotnet test tests/PokerProOS.Tests --filter MatrizDeManosTests
```

Esperado: no compila, `MatrizDeManos` no existe.

- [ ] **Step 4: Implementar `MatrizDeManos`**

```csharp
namespace PokerProOS.Domain.Manos;

/// <summary>
/// La matriz canónica de 13x13 manos iniciales de Hold'em.
/// Los 13 rangos son la única constante legítima del proyecto: el póker no cambia.
/// </summary>
public static class MatrizDeManos
{
    public static IReadOnlyList<char> Rangos { get; } =
        ['A', 'K', 'Q', 'J', 'T', '9', '8', '7', '6', '5', '4', '3', '2'];

    private static readonly IReadOnlyList<string> _todas = Construir();

    public static IReadOnlyList<string> Todas() => _todas;

    public static string Etiqueta(int fila, int columna)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(fila);
        ArgumentOutOfRangeException.ThrowIfNegative(columna);
        ArgumentOutOfRangeException.ThrowIfGreaterThanOrEqual(fila, Rangos.Count);
        ArgumentOutOfRangeException.ThrowIfGreaterThanOrEqual(columna, Rangos.Count);

        var alto = Rangos[Math.Min(fila, columna)];
        var bajo = Rangos[Math.Max(fila, columna)];
        if (fila == columna) return $"{alto}{bajo}";
        return fila < columna ? $"{alto}{bajo}s" : $"{alto}{bajo}o";
    }

    public static IReadOnlyList<string> Vecinas(string etiqueta)
    {
        var (fila, columna) = Coordenadas(etiqueta);
        var vecinas = new List<string>(4);
        foreach (var (df, dc) in new[] { (-1, 0), (1, 0), (0, -1), (0, 1) })
        {
            var f = fila + df;
            var c = columna + dc;
            if (f < 0 || c < 0 || f >= Rangos.Count || c >= Rangos.Count) continue;
            vecinas.Add(Etiqueta(f, c));
        }
        return vecinas;
    }

    private static (int Fila, int Columna) Coordenadas(string etiqueta)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(etiqueta);
        var primero = Rangos.IndexOf(etiqueta[0]);
        var segundo = Rangos.IndexOf(etiqueta[1]);
        if (primero < 0 || segundo < 0)
            throw new ArgumentException($"Mano desconocida: {etiqueta}", nameof(etiqueta));

        if (etiqueta.Length == 2) return (primero, primero);
        return etiqueta[2] switch
        {
            's' => (primero, segundo),
            'o' => (segundo, primero),
            _ => throw new ArgumentException($"Mano desconocida: {etiqueta}", nameof(etiqueta))
        };
    }

    private static List<string> Construir()
    {
        var manos = new List<string>(169);
        for (var fila = 0; fila < Rangos.Count; fila++)
            for (var columna = 0; columna < Rangos.Count; columna++)
                manos.Add(Etiqueta(fila, columna));
        return manos.Distinct().ToList();
    }
}
```

Nota: `Construir` recorre la matriz completa y deduplica, porque la diagonal produce cada pareja una sola vez pero el recorrido visita 169 celdas exactas. Verificar que el conteo dé 169 y no 178.

- [ ] **Step 5: Correr las pruebas y confirmar que pasan**

```bash
dotnet test tests/PokerProOS.Tests --filter MatrizDeManosTests
```

Esperado: 12 pruebas en verde (6 hechos más 6 casos del Theory).

- [ ] **Step 6: Borrar el andamiaje muerto**

```bash
rm src/PokerProOS.Api/WeatherForecast.cs
rm src/PokerProOS.Api/Controllers/WeatherForecastController.cs
rm -r src/PokerProOS.Domain/Enums
rm src/PokerProOS.Domain/ValueObjects/HandLabel.cs
dotnet build PokerProOS.slnx
```

Nada de esto lo referencia nadie; el build lo confirma. `HandLabel` además tenía el regex roto: `^(2-9|T|J|Q|K|A){2}[so]?$` alterna contra la cadena literal `2-9`, así que rechazaba `72o`, `T9s` y `55`, y aceptaba `2-92-9`. Los usos que aparecen al buscar "HandLabel" son la propiedad string homónima de las entidades, no el tipo. Si algo falla, es que quedó un `using PokerProOS.Domain.Enums` huérfano: borrarlo.

- [ ] **Step 7: Commit**

```bash
git add -A
git commit -m "feat: matriz de manos en Domain y proyecto de pruebas

Agrega MatrizDeManos con generacion de las 169 manos y calculo de
vecinas, que despues alimenta la deteccion de borde. Elimina el
andamiaje de dotnet new y los enums sin uso."
```

---

### Task 2: Registro de acciones

**Files:**
- Create: `database/registro/acciones.json`
- Create: `src/PokerProOS.Application/Tablas/IRegistroDeAcciones.cs`
- Create: `src/PokerProOS.Application/Tablas/AccionDefinida.cs`
- Create: `src/PokerProOS.Infrastructure/Tablas/RegistroDeAccionesJson.cs`
- Create: `tests/PokerProOS.Tests/Tablas/RegistroDeAccionesTests.cs`
- Modify: `tests/PokerProOS.Tests/PokerProOS.Tests.csproj` (referencia a Infrastructure)

**Interfaces:**
- Consumes: nada de tareas previas.
- Produces:
  - `record AccionDefinida(string Clave, string Etiqueta, string Color, string ColorTexto, int Orden, IReadOnlyList<string> Dichos)`
  - `interface IRegistroDeAcciones` con `IReadOnlyList<AccionDefinida> Todas { get; }`, `bool Existe(string clave)`, `AccionDefinida Obtener(string clave)` (lanza `KeyNotFoundException` si no existe).
  - `RegistroDeAccionesJson.Cargar(string rutaArchivo)` → `IRegistroDeAcciones`.

- [ ] **Step 1: Crear el archivo de registro**

`database/registro/acciones.json`. Los colores son los del proyecto original y no se negocian: son la memoria visual ya entrenada del usuario.

```json
{
  "acciones": [
    {
      "clave": "ALL-IN",
      "etiqueta": "ALL-IN",
      "color": "#43bf55",
      "colorTexto": "#061018",
      "orden": 1,
      "dichos": ["all in", "allin", "shove", "push", "jam", "empujar", "tirarme"]
    },
    {
      "clave": "CALL",
      "etiqueta": "CALL",
      "color": "#ffb743",
      "colorTexto": "#061018",
      "orden": 2,
      "dichos": ["call", "pagar", "igualar"]
    },
    {
      "clave": "RAISE_X2",
      "etiqueta": "RAISE X2",
      "color": "#7c86dc",
      "colorTexto": "#081025",
      "orden": 3,
      "dichos": ["raise", "raise por dos", "subir", "doblar"]
    },
    {
      "clave": "FOLD",
      "etiqueta": "FOLD",
      "color": "#edf3fb",
      "colorTexto": "#111820",
      "orden": 4,
      "dichos": ["fold", "foldear", "tirar", "botar"]
    }
  ]
}
```

- [ ] **Step 2: Escribir las pruebas que fallan**

```csharp
using PokerProOS.Infrastructure.Tablas;

namespace PokerProOS.Tests.Tablas;

public class RegistroDeAccionesTests
{
    private static string Ruta => Rutas.Registro("acciones.json");

    [Fact]
    public void Carga_las_cuatro_acciones_del_proyecto()
        => Assert.Equal(4, RegistroDeAccionesJson.Cargar(Ruta).Todas.Count);

    [Theory]
    [InlineData("ALL-IN", "#43bf55")]
    [InlineData("CALL", "#ffb743")]
    [InlineData("RAISE_X2", "#7c86dc")]
    [InlineData("FOLD", "#edf3fb")]
    public void Conserva_los_colores_del_proyecto_original(string clave, string color)
        => Assert.Equal(color, RegistroDeAccionesJson.Cargar(Ruta).Obtener(clave).Color);

    [Fact]
    public void Ordena_las_acciones_para_la_leyenda()
    {
        var claves = RegistroDeAccionesJson.Cargar(Ruta).Todas.Select(a => a.Clave);
        Assert.Equal(["ALL-IN", "CALL", "RAISE_X2", "FOLD"], claves);
    }

    [Fact]
    public void Reconoce_una_accion_existente()
        => Assert.True(RegistroDeAccionesJson.Cargar(Ruta).Existe("FOLD"));

    [Fact]
    public void Rechaza_una_accion_inexistente()
        => Assert.False(RegistroDeAccionesJson.Cargar(Ruta).Existe("LIMP"));

    [Fact]
    public void Falla_al_pedir_una_accion_inexistente()
        => Assert.Throws<KeyNotFoundException>(
            () => RegistroDeAccionesJson.Cargar(Ruta).Obtener("LIMP"));

    [Fact]
    public void Cada_accion_declara_al_menos_una_forma_hablada()
        => Assert.All(RegistroDeAccionesJson.Cargar(Ruta).Todas,
            a => Assert.NotEmpty(a.Dichos));
}
```

Y el localizador de archivos de datos, que todas las suites siguientes reutilizan:

```csharp
namespace PokerProOS.Tests;

/// <summary>
/// Ubica la carpeta database/ subiendo desde el directorio de salida
/// hasta encontrar la raíz del repositorio. Evita rutas relativas frágiles
/// como las cinco subidas que tenía el sembrado original.
/// </summary>
public static class Rutas
{
    private static readonly string RaizRepo = Localizar();

    public static string Registro(string archivo)
        => Path.Combine(RaizRepo, "database", "registro", archivo);

    public static string SemillasDeTablas
        => Path.Combine(RaizRepo, "database", "seed-data");

    private static string Localizar()
    {
        var actual = new DirectoryInfo(AppContext.BaseDirectory);
        while (actual is not null)
        {
            if (Directory.Exists(Path.Combine(actual.FullName, "database")))
                return actual.FullName;
            actual = actual.Parent;
        }
        throw new DirectoryNotFoundException(
            "No se encontró la carpeta database/ subiendo desde " + AppContext.BaseDirectory);
    }
}
```

- [ ] **Step 3: Correr y confirmar que fallan**

```bash
dotnet add tests/PokerProOS.Tests reference src/PokerProOS.Infrastructure
dotnet test tests/PokerProOS.Tests --filter RegistroDeAccionesTests
```

Esperado: no compila, `RegistroDeAccionesJson` no existe.

- [ ] **Step 4: Implementar el contrato y la carga**

`src/PokerProOS.Application/Tablas/AccionDefinida.cs`:

```csharp
namespace PokerProOS.Application.Tablas;

public record AccionDefinida(
    string Clave,
    string Etiqueta,
    string Color,
    string ColorTexto,
    int Orden,
    IReadOnlyList<string> Dichos);
```

`src/PokerProOS.Application/Tablas/IRegistroDeAcciones.cs`:

```csharp
namespace PokerProOS.Application.Tablas;

public interface IRegistroDeAcciones
{
    IReadOnlyList<AccionDefinida> Todas { get; }
    bool Existe(string clave);
    AccionDefinida Obtener(string clave);
}
```

`src/PokerProOS.Infrastructure/Tablas/RegistroDeAccionesJson.cs`:

```csharp
using System.Text.Json;
using PokerProOS.Application.Tablas;

namespace PokerProOS.Infrastructure.Tablas;

public sealed class RegistroDeAccionesJson : IRegistroDeAcciones
{
    private readonly Dictionary<string, AccionDefinida> _porClave;

    private RegistroDeAccionesJson(IReadOnlyList<AccionDefinida> acciones)
    {
        Todas = acciones;
        _porClave = acciones.ToDictionary(a => a.Clave, StringComparer.OrdinalIgnoreCase);
    }

    public IReadOnlyList<AccionDefinida> Todas { get; }

    public bool Existe(string clave) => _porClave.ContainsKey(clave);

    public AccionDefinida Obtener(string clave) => _porClave.TryGetValue(clave, out var accion)
        ? accion
        : throw new KeyNotFoundException(
            $"La acción '{clave}' no está en el registro. Agregala a database/registro/acciones.json.");

    public static IRegistroDeAcciones Cargar(string rutaArchivo)
    {
        using var documento = JsonDocument.Parse(File.ReadAllText(rutaArchivo));
        var acciones = documento.RootElement.GetProperty("acciones")
            .EnumerateArray()
            .Select(e => new AccionDefinida(
                e.GetProperty("clave").GetString()!,
                e.GetProperty("etiqueta").GetString()!,
                e.GetProperty("color").GetString()!,
                e.GetProperty("colorTexto").GetString()!,
                e.GetProperty("orden").GetInt32(),
                e.GetProperty("dichos").EnumerateArray().Select(d => d.GetString()!).ToList()))
            .OrderBy(a => a.Orden)
            .ToList();
        return new RegistroDeAccionesJson(acciones);
    }
}
```

- [ ] **Step 5: Correr y confirmar que pasan**

```bash
dotnet test tests/PokerProOS.Tests --filter RegistroDeAccionesTests
```

Esperado: 10 pruebas en verde (6 hechos más 4 casos del `Theory`).

- [ ] **Step 6: Commit**

```bash
git add -A
git commit -m "feat: registro de acciones en datos

Colores, etiquetas y formas habladas de cada accion salen de
database/registro/acciones.json. Agregar una accion nueva deja de
requerir cambios de codigo. Recupera los colores del proyecto original."
```

---

### Task 3: Validador de tablas

**Files:**
- Create: `src/PokerProOS.Infrastructure/Tablas/ValidadorDeTabla.cs`
- Create: `src/PokerProOS.Application/Tablas/ResultadoDeValidacion.cs`
- Create: `tests/PokerProOS.Tests/Tablas/ValidadorDeTablaTests.cs`
- Delete: `src/PokerProOS.Application/Charts/Validators/ChartValidator.cs`, `src/PokerProOS.Application/Charts/Validators/ValidationResult.cs`

**Interfaces:**
- Consumes: `MatrizDeManos.Todas()` (Task 1), `IRegistroDeAcciones` (Task 2).
- Produces:
  - `record ProblemaDeTabla(string Archivo, string Stack, string Spot, string Mensaje)`
  - `record ResultadoDeValidacion(IReadOnlyList<ProblemaDeTabla> Problemas)` con `bool EsValido => Problemas.Count == 0`
  - `ValidadorDeTabla(IRegistroDeAcciones registro)` con `ResultadoDeValidacion Validar(string rutaArchivo)`

Las seis reglas, tomadas del spec: cobertura exacta de 169 manos por spot, sin manos duplicadas entre acciones del mismo spot, toda etiqueta existe en la matriz, toda acción existe en el registro, como máximo un `REST` por spot, y si el archivo declara `expectedCounts` o `checks` deben cuadrar.

- [ ] **Step 1: Escribir las pruebas que fallan**

```csharp
using PokerProOS.Infrastructure.Tablas;

namespace PokerProOS.Tests.Tablas;

public class ValidadorDeTablaTests : IDisposable
{
    private readonly ValidadorDeTabla _validador =
        new(RegistroDeAccionesJson.Cargar(Rutas.Registro("acciones.json")));
    private readonly List<string> _temporales = [];

    [Fact]
    public void Las_once_tablas_reales_del_proyecto_son_validas()
    {
        var archivos = Directory.GetFiles(Rutas.SemillasDeTablas, "*.json");
        Assert.Equal(11, archivos.Length);
        foreach (var archivo in archivos)
        {
            var resultado = _validador.Validar(archivo);
            Assert.True(resultado.EsValido,
                $"{Path.GetFileName(archivo)}: " +
                string.Join(" | ", resultado.Problemas.Select(p => p.Mensaje)));
        }
    }

    [Fact]
    public void Detecta_una_mano_repetida_entre_dos_acciones()
    {
        var ruta = Fabricar("""
            {"situation":{"key":"S","label":"S"},"stacks":[{"key":"5bb","minBB":5,"maxBB":5,
            "spots":[{"key":"SB_OR","label":"SB OR","actions":{
            "CALL":["AA","KK"],"ALL-IN":["AA"],"FOLD":"REST"}}]}]}
            """);
        var problemas = _validador.Validar(ruta).Problemas;
        Assert.Contains(problemas, p => p.Mensaje.Contains("AA") && p.Mensaje.Contains("duplicada"));
    }

    [Fact]
    public void Detecta_una_accion_fuera_del_registro()
    {
        var ruta = Fabricar("""
            {"situation":{"key":"S","label":"S"},"stacks":[{"key":"5bb","minBB":5,"maxBB":5,
            "spots":[{"key":"SB_OR","label":"SB OR","actions":{
            "LIMP":["AA"],"FOLD":"REST"}}]}]}
            """);
        Assert.Contains(_validador.Validar(ruta).Problemas,
            p => p.Mensaje.Contains("LIMP") && p.Mensaje.Contains("registro"));
    }

    [Fact]
    public void Detecta_dos_acciones_marcadas_como_resto()
    {
        var ruta = Fabricar("""
            {"situation":{"key":"S","label":"S"},"stacks":[{"key":"5bb","minBB":5,"maxBB":5,
            "spots":[{"key":"SB_OR","label":"SB OR","actions":{
            "CALL":"REST","FOLD":"REST"}}]}]}
            """);
        Assert.Contains(_validador.Validar(ruta).Problemas, p => p.Mensaje.Contains("REST"));
    }

    [Fact]
    public void Detecta_cobertura_incompleta_sin_resto()
    {
        var ruta = Fabricar("""
            {"situation":{"key":"S","label":"S"},"stacks":[{"key":"5bb","minBB":5,"maxBB":5,
            "spots":[{"key":"SB_OR","label":"SB OR","actions":{
            "CALL":["AA","KK"],"FOLD":["QQ"]}}]}]}
            """);
        Assert.Contains(_validador.Validar(ruta).Problemas,
            p => p.Mensaje.Contains("169") && p.Mensaje.Contains("3"));
    }

    [Fact]
    public void Detecta_una_etiqueta_de_mano_inexistente()
    {
        var ruta = Fabricar("""
            {"situation":{"key":"S","label":"S"},"stacks":[{"key":"5bb","minBB":5,"maxBB":5,
            "spots":[{"key":"SB_OR","label":"SB OR","actions":{
            "CALL":["XZ9"],"FOLD":"REST"}}]}]}
            """);
        Assert.Contains(_validador.Validar(ruta).Problemas, p => p.Mensaje.Contains("XZ9"));
    }

    [Fact]
    public void Detecta_un_conteo_declarado_que_no_cuadra()
    {
        var ruta = Fabricar("""
            {"situation":{"key":"S","label":"S"},"stacks":[{"key":"5bb","minBB":5,"maxBB":5,
            "spots":[{"key":"SB_OR","label":"SB OR","actions":{"CALL":["AA"],"FOLD":"REST"},
            "expectedCounts":{"CALL":99,"FOLD":70,"TOTAL":169}}]}]}
            """);
        Assert.Contains(_validador.Validar(ruta).Problemas,
            p => p.Mensaje.Contains("CALL") && p.Mensaje.Contains("99"));
    }

    [Fact]
    public void Detecta_un_check_declarado_que_no_cuadra()
    {
        var ruta = Fabricar("""
            {"situation":{"key":"S","label":"S"},"stacks":[{"key":"5bb","minBB":5,"maxBB":5,
            "spots":[{"key":"SB_OR","label":"SB OR","actions":{"CALL":["AA"],"FOLD":"REST"},
            "checks":{"AA":"FOLD"}}]}]}
            """);
        Assert.Contains(_validador.Validar(ruta).Problemas, p => p.Mensaje.Contains("AA"));
    }

    [Fact]
    public void Informa_archivo_stack_y_spot_del_problema()
    {
        var ruta = Fabricar("""
            {"situation":{"key":"S","label":"S"},"stacks":[{"key":"9bb","minBB":9,"maxBB":9,
            "spots":[{"key":"VS_BB_3BET","label":"x","actions":{"LIMP":["AA"],"FOLD":"REST"}}]}]}
            """);
        var problema = Assert.Single(_validador.Validar(ruta).Problemas);
        Assert.Equal("9bb", problema.Stack);
        Assert.Equal("VS_BB_3BET", problema.Spot);
        Assert.Equal(Path.GetFileName(ruta), problema.Archivo);
    }

    private string Fabricar(string json)
    {
        var ruta = Path.Combine(Path.GetTempPath(), $"tabla-{Guid.NewGuid():N}.json");
        File.WriteAllText(ruta, json);
        _temporales.Add(ruta);
        return ruta;
    }

    public void Dispose()
    {
        foreach (var ruta in _temporales) File.Delete(ruta);
    }
}
```

- [ ] **Step 2: Correr y confirmar que fallan**

```bash
dotnet test tests/PokerProOS.Tests --filter ValidadorDeTablaTests
```

Esperado: no compila, `ValidadorDeTabla` no existe.

- [ ] **Step 3: Implementar el validador**

`src/PokerProOS.Application/Tablas/ResultadoDeValidacion.cs`. Va en Application, no en Infrastructure: `ICatalogoDeTablas` lo expone en su superficie pública y Application no puede depender de Infrastructure.

```csharp
namespace PokerProOS.Application.Tablas;

public record ProblemaDeTabla(string Archivo, string Stack, string Spot, string Mensaje);

public record ResultadoDeValidacion(IReadOnlyList<ProblemaDeTabla> Problemas)
{
    public bool EsValido => Problemas.Count == 0;
}
```

`src/PokerProOS.Infrastructure/Tablas/ValidadorDeTabla.cs`:

```csharp
using System.Text.Json;
using PokerProOS.Application.Tablas;
using PokerProOS.Domain.Manos;

namespace PokerProOS.Infrastructure.Tablas;

public sealed class ValidadorDeTabla(IRegistroDeAcciones registro)
{
    private static readonly HashSet<string> ManosValidas = MatrizDeManos.Todas().ToHashSet();

    public ResultadoDeValidacion Validar(string rutaArchivo)
    {
        var archivo = Path.GetFileName(rutaArchivo);
        var problemas = new List<ProblemaDeTabla>();

        JsonDocument documento;
        try
        {
            documento = JsonDocument.Parse(File.ReadAllText(rutaArchivo));
        }
        catch (JsonException ex)
        {
            return new ResultadoDeValidacion([new ProblemaDeTabla(archivo, "", "", $"JSON inválido: {ex.Message}")]);
        }

        using (documento)
        {
            if (!documento.RootElement.TryGetProperty("stacks", out var stacks))
                return new ResultadoDeValidacion([new ProblemaDeTabla(archivo, "", "", "Falta la propiedad 'stacks'.")]);

            foreach (var stack in stacks.EnumerateArray())
            {
                var claveStack = stack.GetProperty("key").GetString() ?? "";
                if (!stack.TryGetProperty("spots", out var spots)) continue;

                foreach (var spot in spots.EnumerateArray())
                    ValidarSpot(archivo, claveStack, spot, problemas);
            }
        }

        return new ResultadoDeValidacion(problemas);
    }

    private void ValidarSpot(string archivo, string claveStack, JsonElement spot, List<ProblemaDeTabla> problemas)
    {
        var claveSpot = spot.GetProperty("key").GetString() ?? "";
        void Anotar(string mensaje) => problemas.Add(new ProblemaDeTabla(archivo, claveStack, claveSpot, mensaje));

        if (!spot.TryGetProperty("actions", out var acciones))
        {
            Anotar("El spot no declara 'actions'.");
            return;
        }

        var asignadas = new Dictionary<string, string>();
        string? resto = null;

        foreach (var propiedad in acciones.EnumerateObject())
        {
            var accion = propiedad.Name;

            if (!registro.Existe(accion))
            {
                Anotar($"La acción '{accion}' no está en el registro de acciones.");
                continue;
            }

            if (propiedad.Value.ValueKind == JsonValueKind.String)
            {
                if (propiedad.Value.GetString() != "REST")
                {
                    Anotar($"La acción '{accion}' tiene un valor de texto que no es REST.");
                    continue;
                }
                if (resto is not null)
                {
                    Anotar($"Hay dos acciones marcadas como REST: '{resto}' y '{accion}'. Solo puede haber una.");
                    continue;
                }
                resto = accion;
                continue;
            }

            if (propiedad.Value.ValueKind != JsonValueKind.Array)
            {
                Anotar($"La acción '{accion}' no es ni un arreglo de manos ni REST.");
                continue;
            }

            foreach (var elemento in propiedad.Value.EnumerateArray())
            {
                var mano = elemento.GetString();
                if (mano is null) continue;

                if (!ManosValidas.Contains(mano))
                {
                    Anotar($"La mano '{mano}' no existe en la matriz de 169.");
                    continue;
                }
                if (asignadas.TryGetValue(mano, out var previa))
                {
                    Anotar($"La mano '{mano}' está duplicada: aparece en '{previa}' y en '{accion}'.");
                    continue;
                }
                asignadas[mano] = accion;
            }
        }

        if (resto is not null)
            foreach (var mano in ManosValidas)
                asignadas.TryAdd(mano, resto);

        if (asignadas.Count != 169)
            Anotar($"El spot cubre {asignadas.Count} manos y debe cubrir 169. " +
                   "Falta una acción marcada como REST o faltan manos explícitas.");

        var conteos = asignadas.Values
            .GroupBy(a => a)
            .ToDictionary(g => g.Key, g => g.Count(), StringComparer.OrdinalIgnoreCase);

        if (spot.TryGetProperty("expectedCounts", out var esperados))
            foreach (var esperado in esperados.EnumerateObject())
            {
                var declarado = esperado.Value.GetInt32();
                var real = esperado.Name.Equals("TOTAL", StringComparison.OrdinalIgnoreCase)
                    ? asignadas.Count
                    : conteos.GetValueOrDefault(esperado.Name);
                if (real != declarado)
                    Anotar($"El conteo declarado de '{esperado.Name}' es {declarado} y el real es {real}.");
            }

        if (spot.TryGetProperty("checks", out var comprobaciones))
            foreach (var comprobacion in comprobaciones.EnumerateObject())
            {
                var real = asignadas.GetValueOrDefault(comprobacion.Name);
                var declarada = comprobacion.Value.GetString();
                if (!string.Equals(real, declarada, StringComparison.OrdinalIgnoreCase))
                    Anotar($"El check de '{comprobacion.Name}' declara '{declarada}' y la tabla resuelve '{real}'.");
            }
    }
}
```

- [ ] **Step 4: Correr y confirmar que pasan**

```bash
dotnet test tests/PokerProOS.Tests --filter ValidadorDeTablaTests
```

Esperado: 9 pruebas en verde. La primera es la que importa: las once tablas reales pasan. Ya se verificó fuera de la aplicación que así es, así que un fallo ahí significa un error del validador, no de los datos.

- [ ] **Step 5: Borrar el validador de mentira**

```bash
rm -r src/PokerProOS.Application/Charts/Validators
dotnet build PokerProOS.slnx
```

- [ ] **Step 6: Commit**

```bash
git add -A
git commit -m "feat: validador de tablas real

Implementa las seis reglas del spec y reemplaza el ChartValidator que
siempre aprobaba. Reporta archivo, stack, spot y causa concreta."
```

---

### Task 4: Cargador y catálogo en memoria

**Files:**
- Create: `src/PokerProOS.Domain/Tablas/CeldaDeTabla.cs`
- Create: `src/PokerProOS.Domain/Tablas/RangoDeStack.cs`
- Create: `src/PokerProOS.Application/Tablas/ICatalogoDeTablas.cs`
- Create: `src/PokerProOS.Infrastructure/Tablas/CargadorDeTablas.cs`
- Create: `src/PokerProOS.Infrastructure/Tablas/CatalogoEnMemoria.cs`
- Create: `tests/PokerProOS.Tests/Tablas/CatalogoEnMemoriaTests.cs`
- Delete: `src/PokerProOS.Domain/ValueObjects/StackRange.cs`

**Interfaces:**
- Consumes: `ValidadorDeTabla` (Task 3), `MatrizDeManos` (Task 1).
- Produces:
  - `record RangoDeStack(string Clave, decimal MinBB, decimal MaxBB)` con `bool Cubre(decimal bb)`
  - `record CeldaDeTabla(string Mano, string Accion)`
  - `record SpotDeTabla(string Clave, string Etiqueta, IReadOnlyList<CeldaDeTabla> Celdas)` con `IReadOnlyDictionary<string,int> Conteos` y `string? AccionDe(string mano)`
  - `record TablaDeStack(RangoDeStack Stack, IReadOnlyList<SpotDeTabla> Spots)`
  - `record SituacionDeTabla(string Clave, string Etiqueta, IReadOnlyList<TablaDeStack> Stacks)`
  - `interface ICatalogoDeTablas`: `IReadOnlyList<SituacionDeTabla> Situaciones`, `IReadOnlyList<ProblemaDeTabla> Problemas`, `SituacionDeTabla? Situacion(string clave)`, `TablaDeStack? StackQueCubre(string situacion, decimal bb)`, `TablaDeStack? StackPorClave(string situacion, string claveStack)`, `SpotDeTabla? Spot(string situacion, string claveStack, string claveSpot)`
  - `CargadorDeTablas(ValidadorDeTabla validador)` con `ICatalogoDeTablas CargarDirectorio(string directorio)`

`Problemas` expone los archivos que no pasaron la validación, para que la interfaz los muestre. Un archivo inválido no entra al catálogo pero tampoco impide cargar los demás.

- [ ] **Step 1: Escribir las pruebas que fallan**

```csharp
using PokerProOS.Application.Tablas;
using PokerProOS.Infrastructure.Tablas;

namespace PokerProOS.Tests.Tablas;

public class CatalogoEnMemoriaTests
{
    private static ICatalogoDeTablas Catalogo() =>
        new CargadorDeTablas(new ValidadorDeTabla(
                RegistroDeAccionesJson.Cargar(Rutas.Registro("acciones.json"))))
            .CargarDirectorio(Rutas.SemillasDeTablas);

    [Fact]
    public void Carga_las_once_tablas_sin_problemas()
        => Assert.Empty(Catalogo().Problemas);

    [Fact]
    public void Descubre_la_unica_situacion_existente()
    {
        var situaciones = Catalogo().Situaciones;
        Assert.Single(situaciones);
        Assert.Equal("HU_SB_OR_FISH", situaciones[0].Clave);
    }

    [Fact]
    public void Descubre_los_once_stacks()
        => Assert.Equal(11, Catalogo().Situacion("HU_SB_OR_FISH")!.Stacks.Count);

    [Fact]
    public void Cada_spot_cubre_las_169_manos()
    {
        foreach (var stack in Catalogo().Situacion("HU_SB_OR_FISH")!.Stacks)
            foreach (var spot in stack.Spots)
                Assert.Equal(169, spot.Celdas.Count);
    }

    [Theory]
    [InlineData(7, "7bb")]
    [InlineData(13, "13-16bb")]
    [InlineData(16, "13-16bb")]
    [InlineData(2, "1-4bb")]
    [InlineData(50, "19-99bb")]
    public void Resuelve_el_stack_por_cobertura_y_no_por_texto(decimal bb, string claveEsperada)
        => Assert.Equal(claveEsperada, Catalogo().StackQueCubre("HU_SB_OR_FISH", bb)!.Stack.Clave);

    [Fact]
    public void Devuelve_nulo_para_un_stack_fuera_de_toda_cobertura()
        => Assert.Null(Catalogo().StackQueCubre("HU_SB_OR_FISH", 250));

    [Fact]
    public void Expande_la_accion_marcada_como_resto()
    {
        var spot = Catalogo().Spot("HU_SB_OR_FISH", "10bb", "SB_OR")!;
        Assert.Equal("CALL", spot.AccionDe("AA"));
        Assert.Equal("ALL-IN", spot.AccionDe("A9s"));
        Assert.Equal("CALL", spot.AccionDe("32o"));
    }

    [Fact]
    public void Cuenta_las_manos_por_accion()
    {
        var conteos = Catalogo().Spot("HU_SB_OR_FISH", "10bb", "SB_OR")!.Conteos;
        Assert.Equal(123, conteos["CALL"]);
        Assert.Equal(46, conteos["ALL-IN"]);
        Assert.Equal(169, conteos.Values.Sum());
    }

    [Fact]
    public void Los_stacks_chicos_solo_tienen_tres_spots()
    {
        var catalogo = Catalogo();
        Assert.Equal(3, catalogo.StackPorClave("HU_SB_OR_FISH", "1-4bb")!.Spots.Count);
        Assert.Equal(5, catalogo.StackPorClave("HU_SB_OR_FISH", "6bb")!.Spots.Count);
    }

    [Fact]
    public void Devuelve_nulo_para_un_spot_inexistente_en_ese_stack()
        => Assert.Null(Catalogo().Spot("HU_SB_OR_FISH", "1-4bb", "VS_BB_ISO_3BB"));

    [Fact]
    public void Un_archivo_invalido_no_impide_cargar_los_demas()
    {
        var directorio = Path.Combine(Path.GetTempPath(), $"tablas-{Guid.NewGuid():N}");
        Directory.CreateDirectory(directorio);
        try
        {
            foreach (var archivo in Directory.GetFiles(Rutas.SemillasDeTablas, "*.json"))
                File.Copy(archivo, Path.Combine(directorio, Path.GetFileName(archivo)));
            File.WriteAllText(Path.Combine(directorio, "rota.json"),
                """{"situation":{"key":"X","label":"X"},"stacks":[{"key":"5bb","minBB":5,"maxBB":5,
                   "spots":[{"key":"SB_OR","label":"x","actions":{"LIMP":["AA"],"FOLD":"REST"}}]}]}""");

            var catalogo = new CargadorDeTablas(new ValidadorDeTabla(
                    RegistroDeAccionesJson.Cargar(Rutas.Registro("acciones.json"))))
                .CargarDirectorio(directorio);

            Assert.NotEmpty(catalogo.Problemas);
            Assert.All(catalogo.Problemas, p => Assert.Equal("rota.json", p.Archivo));
            Assert.Equal(11, catalogo.Situacion("HU_SB_OR_FISH")!.Stacks.Count);
        }
        finally
        {
            Directory.Delete(directorio, recursive: true);
        }
    }
}
```

- [ ] **Step 2: Correr y confirmar que fallan**

```bash
dotnet test tests/PokerProOS.Tests --filter CatalogoEnMemoriaTests
```

Esperado: no compila.

- [ ] **Step 3: Implementar los tipos de Domain**

`src/PokerProOS.Domain/Tablas/RangoDeStack.cs`:

```csharp
namespace PokerProOS.Domain.Tablas;

public record RangoDeStack(string Clave, decimal MinBB, decimal MaxBB)
{
    public bool Cubre(decimal bb) => bb >= MinBB && bb <= MaxBB;
}
```

`src/PokerProOS.Domain/Tablas/CeldaDeTabla.cs`:

```csharp
namespace PokerProOS.Domain.Tablas;

public record CeldaDeTabla(string Mano, string Accion);
```

- [ ] **Step 4: Implementar el contrato del catálogo**

`src/PokerProOS.Application/Tablas/ICatalogoDeTablas.cs`:

```csharp
using PokerProOS.Domain.Tablas;

namespace PokerProOS.Application.Tablas;

public record SpotDeTabla(string Clave, string Etiqueta, IReadOnlyList<CeldaDeTabla> Celdas)
{
    private readonly Dictionary<string, string> _porMano =
        Celdas.ToDictionary(c => c.Mano, c => c.Accion, StringComparer.OrdinalIgnoreCase);

    public IReadOnlyDictionary<string, int> Conteos { get; } = Celdas
        .GroupBy(c => c.Accion, StringComparer.OrdinalIgnoreCase)
        .ToDictionary(g => g.Key, g => g.Count(), StringComparer.OrdinalIgnoreCase);

    public string? AccionDe(string mano) => _porMano.GetValueOrDefault(mano);
}

public record TablaDeStack(RangoDeStack Stack, IReadOnlyList<SpotDeTabla> Spots)
{
    public SpotDeTabla? Spot(string clave) =>
        Spots.FirstOrDefault(s => string.Equals(s.Clave, clave, StringComparison.OrdinalIgnoreCase));
}

public record SituacionDeTabla(string Clave, string Etiqueta, IReadOnlyList<TablaDeStack> Stacks);

public interface ICatalogoDeTablas
{
    IReadOnlyList<SituacionDeTabla> Situaciones { get; }
    IReadOnlyList<ProblemaDeTabla> Problemas { get; }
    SituacionDeTabla? Situacion(string clave);
    TablaDeStack? StackQueCubre(string situacion, decimal bb);
    TablaDeStack? StackPorClave(string situacion, string claveStack);
    SpotDeTabla? Spot(string situacion, string claveStack, string claveSpot);
}
```

- [ ] **Step 5: Implementar cargador y catálogo**

`src/PokerProOS.Infrastructure/Tablas/CatalogoEnMemoria.cs`:

```csharp
using PokerProOS.Application.Tablas;

namespace PokerProOS.Infrastructure.Tablas;

public sealed class CatalogoEnMemoria(
    IReadOnlyList<SituacionDeTabla> situaciones,
    IReadOnlyList<ProblemaDeTabla> problemas) : ICatalogoDeTablas
{
    public IReadOnlyList<SituacionDeTabla> Situaciones { get; } = situaciones;
    public IReadOnlyList<ProblemaDeTabla> Problemas { get; } = problemas;

    public SituacionDeTabla? Situacion(string clave) =>
        Situaciones.FirstOrDefault(s => string.Equals(s.Clave, clave, StringComparison.OrdinalIgnoreCase));

    public TablaDeStack? StackQueCubre(string situacion, decimal bb) =>
        Situacion(situacion)?.Stacks.FirstOrDefault(t => t.Stack.Cubre(bb));

    public TablaDeStack? StackPorClave(string situacion, string claveStack) =>
        Situacion(situacion)?.Stacks.FirstOrDefault(
            t => string.Equals(t.Stack.Clave, claveStack, StringComparison.OrdinalIgnoreCase));

    public SpotDeTabla? Spot(string situacion, string claveStack, string claveSpot) =>
        StackPorClave(situacion, claveStack)?.Spot(claveSpot);
}
```

`src/PokerProOS.Infrastructure/Tablas/CargadorDeTablas.cs`:

```csharp
using System.Text.Json;
using PokerProOS.Application.Tablas;
using PokerProOS.Domain.Manos;
using PokerProOS.Domain.Tablas;

namespace PokerProOS.Infrastructure.Tablas;

public sealed class CargadorDeTablas(ValidadorDeTabla validador)
{
    public ICatalogoDeTablas CargarDirectorio(string directorio)
    {
        if (!Directory.Exists(directorio))
            return new CatalogoEnMemoria([], [new ProblemaDeTabla(
                directorio, "", "", $"No existe el directorio de tablas: {directorio}")]);

        var problemas = new List<ProblemaDeTabla>();
        var stacksPorSituacion = new Dictionary<string, (string Etiqueta, List<TablaDeStack> Stacks)>(
            StringComparer.OrdinalIgnoreCase);

        foreach (var archivo in Directory.GetFiles(directorio, "*.json").OrderBy(a => a))
        {
            var validacion = validador.Validar(archivo);
            if (!validacion.EsValido)
            {
                problemas.AddRange(validacion.Problemas);
                continue;
            }
            LeerArchivo(archivo, stacksPorSituacion);
        }

        var situaciones = stacksPorSituacion
            .Select(par => new SituacionDeTabla(
                par.Key,
                par.Value.Etiqueta,
                par.Value.Stacks.OrderBy(t => t.Stack.MinBB).ToList()))
            .ToList();

        return new CatalogoEnMemoria(situaciones, problemas);
    }

    private static void LeerArchivo(
        string archivo,
        Dictionary<string, (string Etiqueta, List<TablaDeStack> Stacks)> acumulador)
    {
        using var documento = JsonDocument.Parse(File.ReadAllText(archivo));
        var raiz = documento.RootElement;
        var situacion = raiz.GetProperty("situation");
        var claveSituacion = situacion.GetProperty("key").GetString()!;
        var etiquetaSituacion = situacion.GetProperty("label").GetString()!;

        if (!acumulador.TryGetValue(claveSituacion, out var entrada))
            acumulador[claveSituacion] = entrada = (etiquetaSituacion, []);

        foreach (var stack in raiz.GetProperty("stacks").EnumerateArray())
        {
            var rango = new RangoDeStack(
                stack.GetProperty("key").GetString()!,
                stack.GetProperty("minBB").GetDecimal(),
                stack.GetProperty("maxBB").GetDecimal());

            var spots = new List<SpotDeTabla>();
            if (stack.TryGetProperty("spots", out var elementosSpot))
                foreach (var spot in elementosSpot.EnumerateArray())
                    spots.Add(LeerSpot(spot));

            entrada.Stacks.Add(new TablaDeStack(rango, spots));
        }
    }

    private static SpotDeTabla LeerSpot(JsonElement spot)
    {
        var asignadas = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        string? resto = null;

        foreach (var propiedad in spot.GetProperty("actions").EnumerateObject())
        {
            if (propiedad.Value.ValueKind == JsonValueKind.String)
            {
                resto = propiedad.Name;
                continue;
            }
            foreach (var elemento in propiedad.Value.EnumerateArray())
                asignadas[elemento.GetString()!] = propiedad.Name;
        }

        if (resto is not null)
            foreach (var mano in MatrizDeManos.Todas())
                asignadas.TryAdd(mano, resto);

        var celdas = MatrizDeManos.Todas()
            .Select(mano => new CeldaDeTabla(mano, asignadas[mano]))
            .ToList();

        return new SpotDeTabla(
            spot.GetProperty("key").GetString()!,
            spot.GetProperty("label").GetString()!,
            celdas);
    }
}
```

`LeerSpot` no revalida: el archivo ya pasó por el validador, así que `asignadas[mano]` no puede fallar. Recorrer `MatrizDeManos.Todas()` en vez del diccionario garantiza además que las celdas salgan en el orden canónico de la matriz.

- [ ] **Step 6: Correr y confirmar que pasan**

```bash
dotnet test tests/PokerProOS.Tests --filter CatalogoEnMemoriaTests
```

Esperado: 15 pruebas en verde.

- [ ] **Step 7: Borrar el value object superado**

```bash
rm src/PokerProOS.Domain/ValueObjects/StackRange.cs
dotnet build PokerProOS.slnx
```

- [ ] **Step 8: Commit**

```bash
git add -A
git commit -m "feat: catalogo de tablas en memoria

Carga y valida los JSON al arrancar, expande REST y resuelve el stack
por cobertura de rango en vez de por igualdad de texto. Un archivo
invalido queda registrado sin impedir la carga del resto."
```

---

### Task 5: Resolver mano con detección de borde

**Files:**
- Create: `src/PokerProOS.Application/Tablas/ResolverManoHandler.cs`
- Create: `src/PokerProOS.Application/Tablas/ConsultaDeMano.cs`
- Create: `tests/PokerProOS.Tests/Tablas/ResolverManoTests.cs`

**Interfaces:**
- Consumes: `ICatalogoDeTablas` (Task 4), `MatrizDeManos.Vecinas` (Task 1).
- Produces:
  - `record ConsultaDeMano(string Situacion, decimal StackBB, string Spot, string RangoAlto, string RangoBajo, string? Palo)`
  - `record RespuestaDeMano(string Mano, string Accion, int ManosEnLaAccion, bool EnElBorde, bool PaloAsumido, string ClaveDeStack)`
  - `enum MotivoSinRespuesta { SituacionDesconocida, StackFueraDeCobertura, SpotInexistente, ManoInvalida }`
  - `record ResultadoDeConsulta(RespuestaDeMano? Respuesta, MotivoSinRespuesta? Motivo, string? Detalle)`
  - `ResolverManoHandler(ICatalogoDeTablas catalogo)` con `ResultadoDeConsulta Resolver(ConsultaDeMano consulta)`

Regla del spec: si `Palo` viene nulo y la mano no es pareja, se asume `o` y `PaloAsumido` queda en `true`. Una mano está en el borde si alguna vecina en la matriz tiene una acción distinta.

- [ ] **Step 1: Escribir las pruebas que fallan**

```csharp
using PokerProOS.Application.Tablas;
using PokerProOS.Infrastructure.Tablas;

namespace PokerProOS.Tests.Tablas;

public class ResolverManoTests
{
    private static ResolverManoHandler Handler() => new(
        new CargadorDeTablas(new ValidadorDeTabla(
                RegistroDeAccionesJson.Cargar(Rutas.Registro("acciones.json"))))
            .CargarDirectorio(Rutas.SemillasDeTablas));

    private static ConsultaDeMano Consulta(
        decimal bb, string alto, string bajo, string? palo = null, string spot = "SB_OR")
        => new("HU_SB_OR_FISH", bb, spot, alto, bajo, palo);

    [Fact]
    public void Resuelve_una_mano_conocida()
    {
        var resultado = Handler().Resolver(Consulta(10, "A", "9", "s"));
        Assert.Equal("A9s", resultado.Respuesta!.Mano);
        Assert.Equal("ALL-IN", resultado.Respuesta.Accion);
    }

    [Fact]
    public void Asume_offsuit_cuando_no_se_dicta_el_palo()
    {
        var resultado = Handler().Resolver(Consulta(10, "A", "K"));
        Assert.Equal("AKo", resultado.Respuesta!.Mano);
        Assert.True(resultado.Respuesta.PaloAsumido);
    }

    [Fact]
    public void No_marca_palo_asumido_en_una_pareja()
    {
        var resultado = Handler().Resolver(Consulta(10, "A", "A"));
        Assert.Equal("AA", resultado.Respuesta!.Mano);
        Assert.False(resultado.Respuesta.PaloAsumido);
    }

    [Fact]
    public void No_marca_palo_asumido_cuando_se_dicto_el_palo()
        => Assert.False(Handler().Resolver(Consulta(10, "A", "9", "s")).Respuesta!.PaloAsumido);

    [Fact]
    public void Ordena_los_rangos_sin_importar_como_se_dictaron()
    {
        var directo = Handler().Resolver(Consulta(10, "A", "9", "s")).Respuesta!.Mano;
        var invertido = Handler().Resolver(Consulta(10, "9", "A", "s")).Respuesta!.Mano;
        Assert.Equal(directo, invertido);
    }

    [Fact]
    public void Encuentra_el_stack_por_cobertura()
        => Assert.Equal("13-16bb", Handler().Resolver(Consulta(15, "A", "A")).Respuesta!.ClaveDeStack);

    [Fact]
    public void Informa_cuantas_manos_tiene_esa_accion()
    {
        var resultado = Handler().Resolver(Consulta(10, "A", "A"));
        Assert.Equal("CALL", resultado.Respuesta!.Accion);
        Assert.Equal(123, resultado.Respuesta.ManosEnLaAccion);
    }

    [Fact]
    public void Marca_como_borde_una_mano_con_vecina_distinta()
    {
        var spot = SpotDeReferencia();
        var mano = spot.Celdas.First(c =>
            PokerProOS.Domain.Manos.MatrizDeManos.Vecinas(c.Mano)
                .Any(v => spot.AccionDe(v) != c.Accion));
        var partes = Descomponer(mano.Mano);
        var resultado = Handler().Resolver(Consulta(10, partes.Alto, partes.Bajo, partes.Palo));
        Assert.True(resultado.Respuesta!.EnElBorde);
    }

    [Fact]
    public void No_marca_como_borde_una_mano_rodeada_de_la_misma_accion()
    {
        var spot = SpotDeReferencia();
        var mano = spot.Celdas.First(c =>
            PokerProOS.Domain.Manos.MatrizDeManos.Vecinas(c.Mano)
                .All(v => spot.AccionDe(v) == c.Accion));
        var partes = Descomponer(mano.Mano);
        var resultado = Handler().Resolver(Consulta(10, partes.Alto, partes.Bajo, partes.Palo));
        Assert.False(resultado.Respuesta!.EnElBorde);
    }

    [Fact]
    public void Avisa_cuando_el_stack_esta_fuera_de_cobertura()
    {
        var resultado = Handler().Resolver(Consulta(250, "A", "A"));
        Assert.Null(resultado.Respuesta);
        Assert.Equal(MotivoSinRespuesta.StackFueraDeCobertura, resultado.Motivo);
    }

    [Fact]
    public void Avisa_cuando_el_spot_no_existe_en_ese_stack()
    {
        var resultado = Handler().Resolver(Consulta(2, "A", "A", spot: "VS_BB_ISO_3BB"));
        Assert.Null(resultado.Respuesta);
        Assert.Equal(MotivoSinRespuesta.SpotInexistente, resultado.Motivo);
    }

    [Fact]
    public void Avisa_cuando_la_situacion_no_existe()
    {
        var resultado = Handler().Resolver(
            new ConsultaDeMano("NO_EXISTE", 10, "SB_OR", "A", "A", null));
        Assert.Equal(MotivoSinRespuesta.SituacionDesconocida, resultado.Motivo);
    }

    private static SpotDeTabla SpotDeReferencia() =>
        new CargadorDeTablas(new ValidadorDeTabla(
                RegistroDeAccionesJson.Cargar(Rutas.Registro("acciones.json"))))
            .CargarDirectorio(Rutas.SemillasDeTablas)
            .Spot("HU_SB_OR_FISH", "10bb", "SB_OR")!;

    private static (string Alto, string Bajo, string? Palo) Descomponer(string mano) =>
        mano.Length == 2
            ? (mano[..1], mano[1..2], null)
            : (mano[..1], mano[1..2], mano[2..3]);
}
```

- [ ] **Step 2: Correr y confirmar que fallan**

```bash
dotnet test tests/PokerProOS.Tests --filter ResolverManoTests
```

- [ ] **Step 3: Implementar**

`src/PokerProOS.Application/Tablas/ConsultaDeMano.cs`:

```csharp
namespace PokerProOS.Application.Tablas;

public record ConsultaDeMano(
    string Situacion,
    decimal StackBB,
    string Spot,
    string RangoAlto,
    string RangoBajo,
    string? Palo);

public record RespuestaDeMano(
    string Mano,
    string Accion,
    int ManosEnLaAccion,
    bool EnElBorde,
    bool PaloAsumido,
    string ClaveDeStack);

public enum MotivoSinRespuesta
{
    SituacionDesconocida,
    StackFueraDeCobertura,
    SpotInexistente,
    ManoInvalida
}

public record ResultadoDeConsulta(
    RespuestaDeMano? Respuesta,
    MotivoSinRespuesta? Motivo,
    string? Detalle);
```

`src/PokerProOS.Application/Tablas/ResolverManoHandler.cs`:

```csharp
using PokerProOS.Domain.Manos;

namespace PokerProOS.Application.Tablas;

public sealed class ResolverManoHandler(ICatalogoDeTablas catalogo)
{
    public ResultadoDeConsulta Resolver(ConsultaDeMano consulta)
    {
        if (catalogo.Situacion(consulta.Situacion) is null)
            return Sin(MotivoSinRespuesta.SituacionDesconocida,
                $"No conozco la situación {consulta.Situacion}.");

        var tabla = catalogo.StackQueCubre(consulta.Situacion, consulta.StackBB);
        if (tabla is null)
            return Sin(MotivoSinRespuesta.StackFueraDeCobertura,
                $"No tengo tabla para {consulta.StackBB} be be.");

        var spot = tabla.Spot(consulta.Spot);
        if (spot is null)
            return Sin(MotivoSinRespuesta.SpotInexistente,
                $"Ese spot no existe a {tabla.Stack.Clave}.");

        var (mano, paloAsumido) = Componer(consulta);
        var accion = spot.AccionDe(mano);
        if (accion is null)
            return Sin(MotivoSinRespuesta.ManoInvalida, $"No reconozco la mano {mano}.");

        var enElBorde = MatrizDeManos.Vecinas(mano)
            .Any(vecina => !string.Equals(spot.AccionDe(vecina), accion, StringComparison.OrdinalIgnoreCase));

        return new ResultadoDeConsulta(
            new RespuestaDeMano(
                mano,
                accion,
                spot.Conteos.GetValueOrDefault(accion),
                enElBorde,
                paloAsumido,
                tabla.Stack.Clave),
            null,
            null);
    }

    /// <summary>
    /// Ordena los rangos de mayor a menor y aplica la regla del spec:
    /// una mano dictada sin palo es offsuit, salvo que sea pareja.
    /// </summary>
    private static (string Mano, bool PaloAsumido) Componer(ConsultaDeMano consulta)
    {
        var indiceAlto = MatrizDeManos.Rangos.IndexOf(consulta.RangoAlto[0]);
        var indiceBajo = MatrizDeManos.Rangos.IndexOf(consulta.RangoBajo[0]);
        var alto = MatrizDeManos.Rangos[Math.Min(indiceAlto, indiceBajo)];
        var bajo = MatrizDeManos.Rangos[Math.Max(indiceAlto, indiceBajo)];

        if (alto == bajo) return ($"{alto}{bajo}", false);

        var palo = consulta.Palo;
        var asumido = string.IsNullOrEmpty(palo);
        return ($"{alto}{bajo}{(asumido ? "o" : palo!.ToLowerInvariant())}", asumido);
    }

    private static ResultadoDeConsulta Sin(MotivoSinRespuesta motivo, string detalle)
        => new(null, motivo, detalle);
}
```

- [ ] **Step 4: Correr y confirmar que pasan**

```bash
dotnet test tests/PokerProOS.Tests --filter ResolverManoTests
```

Esperado: 12 pruebas en verde.

- [ ] **Step 5: Commit**

```bash
git add -A
git commit -m "feat: resolucion de mano con deteccion de borde

Aplica la regla de offsuit por defecto, ordena los rangos dictados en
cualquier orden y marca si la mano tiene alguna vecina con accion
distinta, que es el dato que vale memorizar."
```

---

### Task 6: Registro de vocabulario y redactor de la respuesta

**Files:**
- Create: `database/registro/vocabulario.json`
- Create: `src/PokerProOS.Application/Voz/IRegistroDeVocabulario.cs`
- Create: `src/PokerProOS.Infrastructure/Voz/RegistroDeVocabularioJson.cs`
- Create: `src/PokerProOS.Application/Voz/RedactorDeRespuesta.cs`
- Create: `tests/PokerProOS.Tests/Voz/RedactorDeRespuestaTests.cs`
- Create: `tests/PokerProOS.Tests/Voz/RegistroDeVocabularioTests.cs`

**Interfaces:**
- Consumes: `RespuestaDeMano`, `ResultadoDeConsulta`, `MotivoSinRespuesta` (Task 5); `IRegistroDeAcciones` (Task 2).
- Produces:
  - `record FormasHabladas(string Clave, IReadOnlyList<string> Dichos)`
  - `interface IRegistroDeVocabulario` con `IReadOnlyList<FormasHabladas> Rangos`, `IReadOnlyList<FormasHabladas> Palos`, `IReadOnlyList<FormasHabladas> Spots`, `IReadOnlyList<FormasHabladas> Situaciones`, `IReadOnlyList<string> PalabrasDeStack`
  - `RegistroDeVocabularioJson.Cargar(string ruta)` → `IRegistroDeVocabulario`
  - `RedactorDeRespuesta(IRegistroDeAcciones acciones)` con `string Redactar(ResultadoDeConsulta resultado)`

`PalabrasDeStack` son las palabras que preceden al número al dictar, por ejemplo `be be` y `blinds`. Van en el registro porque dependen de cómo hable el usuario, no del dominio.

- [ ] **Step 1: Crear el vocabulario**

`database/registro/vocabulario.json`. Traslada `docs/voice-dictionary.md` del proyecto anterior a datos. Los rangos llevan tanto la forma española como la inglesa, porque en la mesa se mezclan.

```json
{
  "palabrasDeStack": ["be be", "bb", "blinds", "ciegas"],
  "rangos": [
    { "clave": "A", "dichos": ["as", "a", "ace"] },
    { "clave": "K", "dichos": ["rey", "ka", "king"] },
    { "clave": "Q", "dichos": ["reina", "dama", "cu", "queen"] },
    { "clave": "J", "dichos": ["jota", "jack"] },
    { "clave": "T", "dichos": ["diez", "ten"] },
    { "clave": "9", "dichos": ["nueve", "nine"] },
    { "clave": "8", "dichos": ["ocho", "eight"] },
    { "clave": "7", "dichos": ["siete", "seven"] },
    { "clave": "6", "dichos": ["seis", "six"] },
    { "clave": "5", "dichos": ["cinco", "five"] },
    { "clave": "4", "dichos": ["cuatro", "four"] },
    { "clave": "3", "dichos": ["tres", "three"] },
    { "clave": "2", "dichos": ["dos", "two", "deuce"] }
  ],
  "palos": [
    { "clave": "s", "dichos": ["suited", "del mismo palo", "mismo palo", "color"] },
    { "clave": "o", "dichos": ["offsuit", "off suit", "off", "distinto palo"] }
  ],
  "spots": [
    { "clave": "SB_OR", "dichos": ["ese be o erre", "mi accion", "primera accion", "abrir", "open"] },
    { "clave": "VS_BB_ALL_IN", "dichos": ["contra all in", "versus all in", "contra shove", "be be all in"] },
    { "clave": "VS_BB_3BET", "dichos": ["contra tres bet", "versus tres bet", "contra tribet"] },
    { "clave": "VS_BB_ISO_3BB", "dichos": ["contra iso tres", "versus iso tres be be", "iso tres"] },
    { "clave": "VS_BB_ISO_ALL_IN", "dichos": ["contra iso all in", "versus iso all in", "iso all in"] }
  ],
  "situaciones": [
    { "clave": "HU_SB_OR_FISH", "dichos": ["heads up contra fish", "hu fish", "contra fish", "fish"] }
  ]
}
```

- [ ] **Step 2: Escribir las pruebas del vocabulario y del redactor**

```csharp
using PokerProOS.Infrastructure.Voz;

namespace PokerProOS.Tests.Voz;

public class RegistroDeVocabularioTests
{
    private static IRegistroDeVocabulario Cargar() =>
        RegistroDeVocabularioJson.Cargar(Rutas.Registro("vocabulario.json"));

    [Fact]
    public void Declara_los_trece_rangos()
        => Assert.Equal(13, Cargar().Rangos.Count);

    [Fact]
    public void Declara_los_dos_palos()
        => Assert.Equal(2, Cargar().Palos.Count);

    [Fact]
    public void Cada_rango_tiene_al_menos_una_forma_hablada()
        => Assert.All(Cargar().Rangos, r => Assert.NotEmpty(r.Dichos));

    [Fact]
    public void Las_claves_de_rango_coinciden_con_la_matriz()
    {
        var delVocabulario = Cargar().Rangos.Select(r => r.Clave[0]).OrderBy(c => c);
        var deLaMatriz = PokerProOS.Domain.Manos.MatrizDeManos.Rangos.OrderBy(c => c);
        Assert.Equal(deLaMatriz, delVocabulario);
    }

    [Fact]
    public void Ninguna_forma_hablada_se_repite_entre_rangos()
    {
        var todos = Cargar().Rangos.SelectMany(r => r.Dichos).ToList();
        Assert.Equal(todos.Count, todos.Distinct().Count());
    }

    [Fact]
    public void Los_spots_declarados_existen_en_las_tablas()
    {
        var catalogo = new PokerProOS.Infrastructure.Tablas.CargadorDeTablas(
                new PokerProOS.Infrastructure.Tablas.ValidadorDeTabla(
                    PokerProOS.Infrastructure.Tablas.RegistroDeAccionesJson.Cargar(
                        Rutas.Registro("acciones.json"))))
            .CargarDirectorio(Rutas.SemillasDeTablas);

        var deLasTablas = catalogo.Situaciones
            .SelectMany(s => s.Stacks).SelectMany(t => t.Spots)
            .Select(s => s.Clave).Distinct().ToHashSet();

        Assert.All(Cargar().Spots, s => Assert.Contains(s.Clave, deLasTablas));
    }
}
```

```csharp
using PokerProOS.Application.Tablas;
using PokerProOS.Application.Voz;
using PokerProOS.Infrastructure.Tablas;

namespace PokerProOS.Tests.Voz;

public class RedactorDeRespuestaTests
{
    private static RedactorDeRespuesta Redactor() =>
        new(RegistroDeAccionesJson.Cargar(Rutas.Registro("acciones.json")));

    private static ResultadoDeConsulta Con(
        string mano, string accion, int conteo, bool borde, bool asumido) =>
        new(new RespuestaDeMano(mano, accion, conteo, borde, asumido, "7bb"), null, null);

    [Fact]
    public void Dice_solo_la_accion_cuando_no_hay_nada_que_aclarar()
        => Assert.Equal("ALL-IN.", Redactor().Redactar(Con("A9s", "ALL-IN", 113, false, false)));

    [Fact]
    public void Agrega_el_borde_y_el_conteo_cuando_la_mano_esta_en_el_limite()
        => Assert.Equal("ALL-IN. En el borde, 113 manos.",
            Redactor().Redactar(Con("A9s", "ALL-IN", 113, true, false)));

    [Fact]
    public void Repite_la_mano_cuando_se_asumio_el_palo()
        => Assert.Equal("A K offsuit: CALL.",
            Redactor().Redactar(Con("AKo", "CALL", 43, false, true)));

    [Fact]
    public void Repite_la_mano_y_avisa_del_borde()
        => Assert.Equal("A K offsuit: CALL. En el borde, 43 manos.",
            Redactor().Redactar(Con("AKo", "CALL", 43, true, true)));

    [Fact]
    public void Usa_la_etiqueta_del_registro_y_no_la_clave()
        => Assert.Equal("RAISE X2.", Redactor().Redactar(Con("AA", "RAISE_X2", 5, false, false)));

    [Theory]
    [InlineData(MotivoSinRespuesta.StackFueraDeCobertura, "No tengo tabla para 250 be be.")]
    [InlineData(MotivoSinRespuesta.SpotInexistente, "Ese spot no existe a 1-4bb.")]
    public void Repite_el_detalle_cuando_no_hay_respuesta(MotivoSinRespuesta motivo, string detalle)
        => Assert.Equal(detalle, Redactor().Redactar(new ResultadoDeConsulta(null, motivo, detalle)));

    [Fact]
    public void Dice_que_no_entendio_cuando_no_hay_detalle()
        => Assert.Equal("No te entendí.",
            Redactor().Redactar(new ResultadoDeConsulta(null, MotivoSinRespuesta.ManoInvalida, null)));
}
```

- [ ] **Step 3: Correr y confirmar que fallan**

```bash
dotnet test tests/PokerProOS.Tests --filter "RegistroDeVocabularioTests|RedactorDeRespuestaTests"
```

- [ ] **Step 4: Implementar el registro de vocabulario**

`src/PokerProOS.Application/Voz/IRegistroDeVocabulario.cs`:

```csharp
namespace PokerProOS.Application.Voz;

public record FormasHabladas(string Clave, IReadOnlyList<string> Dichos);

public interface IRegistroDeVocabulario
{
    IReadOnlyList<string> PalabrasDeStack { get; }
    IReadOnlyList<FormasHabladas> Rangos { get; }
    IReadOnlyList<FormasHabladas> Palos { get; }
    IReadOnlyList<FormasHabladas> Spots { get; }
    IReadOnlyList<FormasHabladas> Situaciones { get; }
}
```

`src/PokerProOS.Infrastructure/Voz/RegistroDeVocabularioJson.cs`:

```csharp
using System.Text.Json;
using PokerProOS.Application.Voz;

namespace PokerProOS.Infrastructure.Voz;

public sealed class RegistroDeVocabularioJson : IRegistroDeVocabulario
{
    private RegistroDeVocabularioJson(
        IReadOnlyList<string> palabrasDeStack,
        IReadOnlyList<FormasHabladas> rangos,
        IReadOnlyList<FormasHabladas> palos,
        IReadOnlyList<FormasHabladas> spots,
        IReadOnlyList<FormasHabladas> situaciones)
        => (PalabrasDeStack, Rangos, Palos, Spots, Situaciones)
            = (palabrasDeStack, rangos, palos, spots, situaciones);

    public IReadOnlyList<string> PalabrasDeStack { get; }
    public IReadOnlyList<FormasHabladas> Rangos { get; }
    public IReadOnlyList<FormasHabladas> Palos { get; }
    public IReadOnlyList<FormasHabladas> Spots { get; }
    public IReadOnlyList<FormasHabladas> Situaciones { get; }

    public static IRegistroDeVocabulario Cargar(string ruta)
    {
        using var documento = JsonDocument.Parse(File.ReadAllText(ruta));
        var raiz = documento.RootElement;

        static IReadOnlyList<FormasHabladas> Leer(JsonElement raiz, string propiedad) =>
            raiz.GetProperty(propiedad).EnumerateArray()
                .Select(e => new FormasHabladas(
                    e.GetProperty("clave").GetString()!,
                    e.GetProperty("dichos").EnumerateArray().Select(d => d.GetString()!).ToList()))
                .ToList();

        return new RegistroDeVocabularioJson(
            raiz.GetProperty("palabrasDeStack").EnumerateArray().Select(e => e.GetString()!).ToList(),
            Leer(raiz, "rangos"),
            Leer(raiz, "palos"),
            Leer(raiz, "spots"),
            Leer(raiz, "situaciones"));
    }
}
```

- [ ] **Step 5: Implementar el redactor**

`src/PokerProOS.Application/Voz/RedactorDeRespuesta.cs`:

```csharp
using PokerProOS.Application.Tablas;

namespace PokerProOS.Application.Voz;

/// <summary>
/// Arma la frase que se va a hablar. La regla del spec: acción sola cuando no
/// hay nada que aclarar, y repetir la mano solo cuando se asumió el palo, que
/// es cuando pudo haberse perdido la palabra "suited" en el reconocimiento.
/// </summary>
public sealed class RedactorDeRespuesta(IRegistroDeAcciones acciones)
{
    public string Redactar(ResultadoDeConsulta resultado)
    {
        if (resultado.Respuesta is null)
            return resultado.Detalle ?? "No te entendí.";

        var r = resultado.Respuesta;
        var etiqueta = acciones.Existe(r.Accion) ? acciones.Obtener(r.Accion).Etiqueta : r.Accion;

        var frase = r.PaloAsumido
            ? $"{Deletrear(r.Mano)}: {etiqueta}."
            : $"{etiqueta}.";

        if (r.EnElBorde)
            frase += $" En el borde, {r.ManosEnLaAccion} manos.";

        return frase;
    }

    /// <summary>Separa la mano para que la síntesis no lea "AKo" como una palabra.</summary>
    private static string Deletrear(string mano)
    {
        var rangos = $"{mano[0]} {mano[1]}";
        if (mano.Length == 2) return rangos;
        return mano[2] == 's' ? $"{rangos} suited" : $"{rangos} offsuit";
    }
}
```

- [ ] **Step 6: Correr y confirmar que pasan**

```bash
dotnet test tests/PokerProOS.Tests --filter "RegistroDeVocabularioTests|RedactorDeRespuestaTests"
```

Esperado: 14 pruebas en verde.

- [ ] **Step 7: Commit**

```bash
git add -A
git commit -m "feat: registro de vocabulario y redactor de respuesta

Traslada el diccionario de voz del proyecto anterior a datos. El
redactor repite la mano solo cuando se asumio el palo, que es cuando
pudo perderse la palabra suited."
```

---

### Task 7: Gramática y motor de voz SAPI

**Files:**
- Create: `src/PokerProOS.Voz.Sapi/PokerProOS.Voz.Sapi.csproj`
- Create: `src/PokerProOS.Voz.Sapi/GeneradorDeGramatica.cs`
- Create: `src/PokerProOS.Voz.Sapi/ReconocedorSapi.cs`
- Create: `src/PokerProOS.Voz.Sapi/SintetizadorSapi.cs`
- Create: `src/PokerProOS.Application/Voz/IReconocedorDeVoz.cs`
- Create: `src/PokerProOS.Application/Voz/ISintetizadorDeVoz.cs`
- Create: `src/PokerProOS.Application/Voz/OpcionesDeVoz.cs`
- Create: `tests/PokerProOS.Tests/Voz/ReconocedorSapiTests.cs`
- Modify: `PokerProOS.slnx`

**Interfaces:**
- Consumes: `ICatalogoDeTablas` (Task 4), `IRegistroDeVocabulario` (Task 6).
- Produces:
  - `record DictadoReconocido(decimal? StackBB, string? Spot, string? Situacion, string RangoAlto, string RangoBajo, string? Palo, float Confianza, string TextoCrudo)`
  - `interface IReconocedorDeVoz : IDisposable` con `event EventHandler<DictadoReconocido>? Reconocido`, `event EventHandler<string>? NoReconocido`, `void ComenzarEscuchaContinua()`, `void Pausar()`, `void Reanudar()`, `DictadoReconocido? ReconocerArchivo(string rutaWav)`
  - `interface ISintetizadorDeVoz : IDisposable` con `void Hablar(string texto)`, `void HablarAArchivo(string texto, string rutaWav)`
  - `record OpcionesDeVoz { string Cultura = "es-ES"; string? Voz = null; float ConfianzaMinima = 0.35f; }`
  - `GeneradorDeGramatica(ICatalogoDeTablas catalogo, IRegistroDeVocabulario vocabulario)` con `Grammar Construir()`

Las claves semánticas de la gramática, que `ReconocedorSapi` lee del resultado: `stack`, `alta`, `baja`, `palo`, `spot`, `situacion`.

- [ ] **Step 1: Crear el proyecto de voz**

```bash
cd "c:/Users/BilanzSmoka/Pictures/Poker"
dotnet new classlib -o src/PokerProOS.Voz.Sapi --framework net10.0
dotnet add src/PokerProOS.Voz.Sapi package System.Speech
dotnet add src/PokerProOS.Voz.Sapi reference src/PokerProOS.Application
dotnet add tests/PokerProOS.Tests reference src/PokerProOS.Voz.Sapi
```

Editar `src/PokerProOS.Voz.Sapi/PokerProOS.Voz.Sapi.csproj` y fijar `<TargetFramework>net10.0-windows</TargetFramework>`. Sin eso el compilador emite `CA1416` en cada llamada a SAPI. Agregar el proyecto a `PokerProOS.slnx` bajo `/src/`.

- [ ] **Step 2: Escribir las pruebas que fallan**

Estas pruebas usan el hallazgo del spike: se sintetiza la frase a WAV y se la alimenta al reconocedor. No hace falta micrófono.

```csharp
using PokerProOS.Application.Voz;
using PokerProOS.Infrastructure.Tablas;
using PokerProOS.Infrastructure.Voz;
using PokerProOS.Voz.Sapi;

namespace PokerProOS.Tests.Voz;

public class ReconocedorSapiTests : IDisposable
{
    private readonly List<string> _temporales = [];

    private static (IReconocedorDeVoz Reconocedor, ISintetizadorDeVoz Sintetizador) Armar()
    {
        var catalogo = new CargadorDeTablas(new ValidadorDeTabla(
                RegistroDeAccionesJson.Cargar(Rutas.Registro("acciones.json"))))
            .CargarDirectorio(Rutas.SemillasDeTablas);
        var vocabulario = RegistroDeVocabularioJson.Cargar(Rutas.Registro("vocabulario.json"));
        // Umbral bajo: sobre audio sintetico la confianza queda entre 0,48 y 0,64.
        var opciones = new OpcionesDeVoz { ConfianzaMinima = 0.20f, Voz = "Microsoft Helena Desktop" };
        var gramatica = new GeneradorDeGramatica(catalogo, vocabulario);
        return (new ReconocedorSapi(gramatica, opciones), new SintetizadorSapi(opciones));
    }

    [Theory]
    [InlineData("siete be be a cinco offsuit", 7, "A", "5", "o")]
    [InlineData("diez be be rey jota suited", 10, "K", "J", "s")]
    [InlineData("cinco be be as as", 5, "A", "A", null)]
    [InlineData("quince be be reina nueve suited", 15, "Q", "9", "s")]
    public void Reconoce_una_frase_dictada(
        string frase, int stack, string alta, string baja, string? palo)
    {
        var (reconocedor, sintetizador) = Armar();
        using (reconocedor)
        using (sintetizador)
        {
            var dictado = reconocedor.ReconocerArchivo(Sintetizar(sintetizador, frase));
            Assert.NotNull(dictado);
            Assert.Equal(stack, dictado!.StackBB);
            Assert.Equal(alta, dictado.RangoAlto);
            Assert.Equal(baja, dictado.RangoBajo);
            Assert.Equal(palo, dictado.Palo);
        }
    }

    [Fact]
    public void Deja_el_stack_nulo_cuando_no_se_dicta()
    {
        var (reconocedor, sintetizador) = Armar();
        using (reconocedor)
        using (sintetizador)
        {
            var dictado = reconocedor.ReconocerArchivo(Sintetizar(sintetizador, "as rey offsuit"));
            Assert.NotNull(dictado);
            Assert.Null(dictado!.StackBB);
            Assert.Equal("A", dictado.RangoAlto);
        }
    }

    [Fact]
    public void No_reconoce_una_frase_fuera_de_la_gramatica()
    {
        var (reconocedor, sintetizador) = Armar();
        using (reconocedor)
        using (sintetizador)
        {
            var wav = Sintetizar(sintetizador, "mañana voy al supermercado a comprar pan");
            Assert.Null(reconocedor.ReconocerArchivo(wav));
        }
    }

    [Fact]
    public void La_gramatica_se_construye_desde_el_catalogo()
    {
        var (reconocedor, sintetizador) = Armar();
        using (reconocedor)
        using (sintetizador)
        {
            // 19-99bb existe en las tablas, asi que 80 be be debe entrar en la gramatica.
            var dictado = reconocedor.ReconocerArchivo(
                Sintetizar(sintetizador, "ochenta be be as rey offsuit"));
            Assert.NotNull(dictado);
            Assert.Equal(80, dictado!.StackBB);
        }
    }

    private string Sintetizar(ISintetizadorDeVoz sintetizador, string frase)
    {
        var ruta = Path.Combine(Path.GetTempPath(), $"voz-{Guid.NewGuid():N}.wav");
        sintetizador.HablarAArchivo(frase, ruta);
        _temporales.Add(ruta);
        return ruta;
    }

    public void Dispose()
    {
        foreach (var ruta in _temporales)
            if (File.Exists(ruta)) File.Delete(ruta);
    }
}
```

- [ ] **Step 3: Correr y confirmar que fallan**

```bash
dotnet test tests/PokerProOS.Tests --filter ReconocedorSapiTests
```

- [ ] **Step 4: Implementar los contratos en Application**

`src/PokerProOS.Application/Voz/IReconocedorDeVoz.cs`:

```csharp
namespace PokerProOS.Application.Voz;

public record DictadoReconocido(
    decimal? StackBB,
    string? Spot,
    string? Situacion,
    string RangoAlto,
    string RangoBajo,
    string? Palo,
    float Confianza,
    string TextoCrudo);

public interface IReconocedorDeVoz : IDisposable
{
    event EventHandler<DictadoReconocido>? Reconocido;
    event EventHandler<string>? NoReconocido;
    void ComenzarEscuchaContinua();
    void Pausar();
    void Reanudar();
    DictadoReconocido? ReconocerArchivo(string rutaWav);
}
```

`src/PokerProOS.Application/Voz/ISintetizadorDeVoz.cs`:

```csharp
namespace PokerProOS.Application.Voz;

public interface ISintetizadorDeVoz : IDisposable
{
    void Hablar(string texto);
    void HablarAArchivo(string texto, string rutaWav);
}
```

`src/PokerProOS.Application/Voz/OpcionesDeVoz.cs`:

```csharp
namespace PokerProOS.Application.Voz;

public record OpcionesDeVoz
{
    public string Cultura { get; init; } = "es-ES";
    public string? Voz { get; init; }
    /// <summary>
    /// Umbral por debajo del cual se descarta el reconocimiento. Sobre audio
    /// sintetizado la confianza real medida queda entre 0,48 y 0,64, así que
    /// las pruebas lo bajan. Configurable, nunca fijo en código.
    /// </summary>
    public float ConfianzaMinima { get; init; } = 0.35f;
}
```

- [ ] **Step 5: Implementar el generador de gramática**

`src/PokerProOS.Voz.Sapi/GeneradorDeGramatica.cs`:

```csharp
using System.Globalization;
using System.Speech.Recognition;
using PokerProOS.Application.Tablas;
using PokerProOS.Application.Voz;

namespace PokerProOS.Voz.Sapi;

/// <summary>
/// Construye la gramática restringida a partir del catálogo y los registros.
/// Nada de listas en código: al agregar una tabla de un stack nuevo, la voz
/// lo entiende sin tocar nada.
/// </summary>
public sealed class GeneradorDeGramatica(
    ICatalogoDeTablas catalogo,
    IRegistroDeVocabulario vocabulario)
{
    public Grammar Construir()
    {
        var cultura = new CultureInfo("es-ES");

        var constructor = new GrammarBuilder { Culture = cultura };
        constructor.Append(new SemanticResultKey("situacion", Formas(vocabulario.Situaciones)), 0, 1);
        constructor.Append(new SemanticResultKey("stack", Stacks()), 0, 1);
        constructor.Append(Choices(vocabulario.PalabrasDeStack), 0, 1);
        constructor.Append(new SemanticResultKey("alta", Formas(vocabulario.Rangos)));
        constructor.Append(new SemanticResultKey("baja", Formas(vocabulario.Rangos)));
        constructor.Append(new SemanticResultKey("palo", Formas(vocabulario.Palos)), 0, 1);
        constructor.Append(new SemanticResultKey("spot", Formas(vocabulario.Spots)), 0, 1);

        return new Grammar(constructor) { Name = "consulta-de-mano" };
    }

    /// <summary>
    /// Los números de stack salen de la cobertura real de las tablas: se toma
    /// el mínimo y el máximo entero que alguna tabla cubre.
    /// </summary>
    private Choices Stacks()
    {
        var rangos = catalogo.Situaciones
            .SelectMany(s => s.Stacks)
            .Select(t => t.Stack)
            .ToList();

        var opciones = new Choices();
        if (rangos.Count == 0) return opciones;

        var minimo = (int)Math.Floor(rangos.Min(r => r.MinBB));
        var maximo = (int)Math.Ceiling(rangos.Max(r => r.MaxBB));

        for (var bb = minimo; bb <= maximo; bb++)
            opciones.Add(new SemanticResultValue(
                bb.ToString(CultureInfo.InvariantCulture), bb));

        return opciones;
    }

    private static Choices Formas(IReadOnlyList<FormasHabladas> formas)
    {
        // Choices no acepta SemanticResultValue[] en el constructor:
        // hay que instanciar vacio y usar Add en bucle.
        var opciones = new Choices();
        foreach (var forma in formas)
            foreach (var dicho in forma.Dichos)
                opciones.Add(new SemanticResultValue(dicho, forma.Clave));
        return opciones;
    }

    private static Choices Choices(IReadOnlyList<string> palabras)
    {
        var opciones = new Choices();
        foreach (var palabra in palabras) opciones.Add(palabra);
        return opciones;
    }
}
```

- [ ] **Step 6: Implementar reconocedor y sintetizador**

`src/PokerProOS.Voz.Sapi/SintetizadorSapi.cs`:

```csharp
using System.Speech.Synthesis;
using PokerProOS.Application.Voz;

namespace PokerProOS.Voz.Sapi;

public sealed class SintetizadorSapi : ISintetizadorDeVoz
{
    private readonly SpeechSynthesizer _sintetizador = new();

    public SintetizadorSapi(OpcionesDeVoz opciones)
    {
        if (!string.IsNullOrWhiteSpace(opciones.Voz))
            _sintetizador.SelectVoice(opciones.Voz);
    }

    public void Hablar(string texto)
    {
        _sintetizador.SetOutputToDefaultAudioDevice();
        _sintetizador.Speak(texto);
    }

    public void HablarAArchivo(string texto, string rutaWav)
    {
        _sintetizador.SetOutputToWaveFile(rutaWav);
        _sintetizador.Speak(texto);
        // Libera el archivo: sin esto un File.Delete posterior falla.
        _sintetizador.SetOutputToNull();
    }

    public void Dispose() => _sintetizador.Dispose();
}
```

`src/PokerProOS.Voz.Sapi/ReconocedorSapi.cs`:

```csharp
using System.Globalization;
using System.Speech.Recognition;
using PokerProOS.Application.Voz;

namespace PokerProOS.Voz.Sapi;

public sealed class ReconocedorSapi : IReconocedorDeVoz
{
    private readonly SpeechRecognitionEngine _motor;
    private readonly OpcionesDeVoz _opciones;
    private bool _escuchaContinua;

    public ReconocedorSapi(GeneradorDeGramatica generador, OpcionesDeVoz opciones)
    {
        _opciones = opciones;
        _motor = new SpeechRecognitionEngine(new CultureInfo(opciones.Cultura));
        _motor.LoadGrammar(generador.Construir());
        _motor.SpeechRecognized += AlReconocer;
        _motor.SpeechRecognitionRejected += (_, _) => NoReconocido?.Invoke(this, "");
        // Windows corta la escucha continua tras un rato de silencio.
        // Reengancharla en RecognizeCompleted es el watchdog.
        _motor.RecognizeCompleted += AlCompletar;
    }

    public event EventHandler<DictadoReconocido>? Reconocido;
    public event EventHandler<string>? NoReconocido;

    public void ComenzarEscuchaContinua()
    {
        _escuchaContinua = true;
        _motor.SetInputToDefaultAudioDevice();
        _motor.RecognizeAsync(RecognizeMode.Multiple);
    }

    public void Pausar()
    {
        if (_escuchaContinua) _motor.RecognizeAsyncCancel();
    }

    public void Reanudar()
    {
        if (_escuchaContinua) _motor.RecognizeAsync(RecognizeMode.Multiple);
    }

    public DictadoReconocido? ReconocerArchivo(string rutaWav)
    {
        _motor.SetInputToWaveFile(rutaWav);
        var resultado = _motor.Recognize();
        // Libera el WAV antes de que el llamador intente borrarlo.
        _motor.SetInputToNull();
        return Interpretar(resultado);
    }

    private void AlReconocer(object? remitente, SpeechRecognizedEventArgs argumentos)
    {
        var dictado = Interpretar(argumentos.Result);
        if (dictado is null) NoReconocido?.Invoke(this, argumentos.Result?.Text ?? "");
        else Reconocido?.Invoke(this, dictado);
    }

    private void AlCompletar(object? remitente, RecognizeCompletedEventArgs argumentos)
    {
        if (_escuchaContinua && !argumentos.Cancelled)
            _motor.RecognizeAsync(RecognizeMode.Multiple);
    }

    private DictadoReconocido? Interpretar(RecognitionResult? resultado)
    {
        if (resultado is null || resultado.Confidence < _opciones.ConfianzaMinima) return null;

        var semantica = resultado.Semantics;
        string? Texto(string clave) =>
            semantica.ContainsKey(clave) ? semantica[clave].Value?.ToString() : null;

        var alta = Texto("alta");
        var baja = Texto("baja");
        if (alta is null || baja is null) return null;

        decimal? stack = Texto("stack") is { } crudo &&
                         decimal.TryParse(crudo, NumberStyles.Any, CultureInfo.InvariantCulture, out var bb)
            ? bb
            : null;

        return new DictadoReconocido(
            stack, Texto("spot"), Texto("situacion"),
            alta, baja, Texto("palo"),
            resultado.Confidence, resultado.Text);
    }

    public void Dispose()
    {
        _escuchaContinua = false;
        _motor.Dispose();
    }
}
```

- [ ] **Step 7: Correr y confirmar que pasan**

```bash
dotnet test tests/PokerProOS.Tests --filter ReconocedorSapiTests
```

Esperado: 12 pruebas en verde (6 hechos más 6 casos del Theory). Si alguna frase no se reconoce, bajar `ConfianzaMinima` en la prueba antes de tocar la gramática: la voz sintética es peor entrada que la voz real.

- [ ] **Step 8: Commit**

```bash
git add -A
git commit -m "feat: reconocimiento y sintesis de voz local con SAPI

Gramatica restringida generada del catalogo, con valores semanticos
que devuelven datos estructurados en vez de texto para parsear.
Las pruebas sintetizan la frase a WAV, asi que corren sin microfono."
```

---

### Task 8: Bucle del copiloto y memoria de contexto

**Files:**
- Create: `src/PokerProOS.Application/Voz/MemoriaDeContexto.cs`
- Create: `src/PokerProOS.Application/Voz/CopilotoDeVoz.cs`
- Create: `tests/PokerProOS.Tests/Voz/CopilotoDeVozTests.cs`
- Create: `tests/PokerProOS.Tests/Voz/DoblesDeVoz.cs`

**Interfaces:**
- Consumes: `IReconocedorDeVoz`, `ISintetizadorDeVoz` (Task 7); `ResolverManoHandler` (Task 5); `RedactorDeRespuesta` (Task 6).
- Produces:
  - `class MemoriaDeContexto` con `string Situacion { get; set; }`, `decimal StackBB { get; set; }`, `string Spot { get; set; }`, `void Aplicar(DictadoReconocido dictado)`
  - `record EventoDeCopiloto(string TextoCrudo, string ManoInterpretada, string Respuesta, bool Resuelta, string? Situacion, string? ClaveDeStack, string? Spot)`
  - `class CopilotoDeVoz` con `event EventHandler<EventoDeCopiloto>? Publicado`, `void Conectar()`, `EventoDeCopiloto Procesar(DictadoReconocido dictado)`

`Procesar` es público y sincrónico a propósito: es lo que hace que el bucle se pueda probar sin audio.

- [ ] **Step 1: Escribir los dobles**

```csharp
using PokerProOS.Application.Voz;

namespace PokerProOS.Tests.Voz;

public sealed class ReconocedorFalso : IReconocedorDeVoz
{
    public bool Escuchando { get; private set; }
    public bool Pausado { get; private set; }

    public event EventHandler<DictadoReconocido>? Reconocido;
    public event EventHandler<string>? NoReconocido;

    public void ComenzarEscuchaContinua() => Escuchando = true;
    public void Pausar() => Pausado = true;
    public void Reanudar() => Pausado = false;
    public DictadoReconocido? ReconocerArchivo(string rutaWav) => null;

    public void Emitir(DictadoReconocido dictado) => Reconocido?.Invoke(this, dictado);
    public void EmitirFallo(string texto) => NoReconocido?.Invoke(this, texto);
    public void Dispose() { }
}

public sealed class SintetizadorFalso : ISintetizadorDeVoz
{
    public List<string> Dicho { get; } = [];
    public List<bool> PausadoAlHablar { get; } = [];
    public ReconocedorFalso? Reconocedor { get; set; }

    public void Hablar(string texto)
    {
        Dicho.Add(texto);
        PausadoAlHablar.Add(Reconocedor?.Pausado ?? false);
    }

    public void HablarAArchivo(string texto, string rutaWav) => Dicho.Add(texto);
    public void Dispose() { }
}
```

- [ ] **Step 2: Escribir las pruebas que fallan**

```csharp
using PokerProOS.Application.Tablas;
using PokerProOS.Application.Voz;
using PokerProOS.Infrastructure.Tablas;

namespace PokerProOS.Tests.Voz;

public class CopilotoDeVozTests
{
    private static (CopilotoDeVoz Copiloto, ReconocedorFalso Reconocedor,
                    SintetizadorFalso Sintetizador, MemoriaDeContexto Memoria) Armar()
    {
        var acciones = RegistroDeAccionesJson.Cargar(Rutas.Registro("acciones.json"));
        var catalogo = new CargadorDeTablas(new ValidadorDeTabla(acciones))
            .CargarDirectorio(Rutas.SemillasDeTablas);
        var reconocedor = new ReconocedorFalso();
        var sintetizador = new SintetizadorFalso { Reconocedor = reconocedor };
        var memoria = new MemoriaDeContexto
        {
            Situacion = "HU_SB_OR_FISH", StackBB = 7, Spot = "SB_OR"
        };
        var copiloto = new CopilotoDeVoz(
            reconocedor, sintetizador,
            new ResolverManoHandler(catalogo),
            new RedactorDeRespuesta(acciones),
            memoria);
        copiloto.Conectar();
        return (copiloto, reconocedor, sintetizador, memoria);
    }

    private static DictadoReconocido Dictado(
        string alta, string baja, string? palo = null,
        decimal? stack = null, string? spot = null) =>
        new(stack, spot, null, alta, baja, palo, 0.9f, $"{alta} {baja}");

    [Fact]
    public void Usa_el_contexto_en_pantalla_cuando_el_dictado_no_trae_stack()
    {
        var (_, reconocedor, sintetizador, _) = Armar();
        reconocedor.Emitir(Dictado("A", "A"));
        Assert.Single(sintetizador.Dicho);
        Assert.Contains("CALL", sintetizador.Dicho[0]);
    }

    [Fact]
    public void Actualiza_el_contexto_cuando_el_dictado_trae_stack()
    {
        var (_, reconocedor, _, memoria) = Armar();
        reconocedor.Emitir(Dictado("A", "A", stack: 15));
        Assert.Equal(15, memoria.StackBB);
    }

    [Fact]
    public void Actualiza_el_contexto_cuando_el_dictado_trae_spot()
    {
        var (_, reconocedor, _, memoria) = Armar();
        reconocedor.Emitir(Dictado("A", "A", spot: "VS_BB_ALL_IN"));
        Assert.Equal("VS_BB_ALL_IN", memoria.Spot);
    }

    [Fact]
    public void Conserva_el_contexto_entre_consultas_sucesivas()
    {
        var (_, reconocedor, _, memoria) = Armar();
        reconocedor.Emitir(Dictado("A", "A", stack: 15));
        reconocedor.Emitir(Dictado("K", "Q", "s"));
        Assert.Equal(15, memoria.StackBB);
    }

    [Fact]
    public void Pausa_el_reconocedor_mientras_habla_para_no_oirse()
    {
        var (_, reconocedor, sintetizador, _) = Armar();
        reconocedor.Emitir(Dictado("A", "A"));
        Assert.True(sintetizador.PausadoAlHablar[0]);
        Assert.False(reconocedor.Pausado);
    }

    [Fact]
    public void Avisa_cuando_no_entendio()
    {
        var (_, reconocedor, sintetizador, _) = Armar();
        reconocedor.EmitirFallo("ruido");
        Assert.Equal("No te entendí.", sintetizador.Dicho[0]);
    }

    [Fact]
    public void Avisa_cuando_el_spot_no_existe_en_ese_stack()
    {
        var (_, reconocedor, sintetizador, _) = Armar();
        reconocedor.Emitir(Dictado("A", "A", stack: 2, spot: "VS_BB_ISO_3BB"));
        Assert.Contains("no existe", sintetizador.Dicho[0]);
    }

    [Fact]
    public void Publica_un_evento_con_la_mano_interpretada()
    {
        var (copiloto, reconocedor, _, _) = Armar();
        EventoDeCopiloto? capturado = null;
        copiloto.Publicado += (_, e) => capturado = e;
        reconocedor.Emitir(Dictado("A", "K"));
        Assert.Equal("AKo", capturado!.ManoInterpretada);
        Assert.True(capturado.Resuelta);
    }

    [Fact]
    public void Publica_un_evento_aunque_no_haya_resuelto()
    {
        var (copiloto, reconocedor, _, _) = Armar();
        EventoDeCopiloto? capturado = null;
        copiloto.Publicado += (_, e) => capturado = e;
        reconocedor.EmitirFallo("ruido");
        Assert.False(capturado!.Resuelta);
    }
}
```

- [ ] **Step 3: Correr y confirmar que fallan**

```bash
dotnet test tests/PokerProOS.Tests --filter CopilotoDeVozTests
```

- [ ] **Step 4: Implementar la memoria de contexto**

`src/PokerProOS.Application/Voz/MemoriaDeContexto.cs`:

```csharp
namespace PokerProOS.Application.Voz;

/// <summary>
/// Guarda el stack y el spot activos para que no haya que repetirlos en cada
/// consulta. Si el dictado los trae, se actualizan; si no, se reutilizan.
/// </summary>
public sealed class MemoriaDeContexto
{
    public string Situacion { get; set; } = "";
    public decimal StackBB { get; set; }
    public string Spot { get; set; } = "";

    public void Aplicar(DictadoReconocido dictado)
    {
        if (dictado.Situacion is { Length: > 0 } situacion) Situacion = situacion;
        if (dictado.StackBB is { } stack) StackBB = stack;
        if (dictado.Spot is { Length: > 0 } spot) Spot = spot;
    }
}
```

- [ ] **Step 5: Implementar el copiloto**

`src/PokerProOS.Application/Voz/CopilotoDeVoz.cs`:

```csharp
using PokerProOS.Application.Tablas;

namespace PokerProOS.Application.Voz;

public record EventoDeCopiloto(
    string TextoCrudo,
    string ManoInterpretada,
    string Respuesta,
    bool Resuelta,
    string? Situacion,
    string? ClaveDeStack,
    string? Spot);

public sealed class CopilotoDeVoz(
    IReconocedorDeVoz reconocedor,
    ISintetizadorDeVoz sintetizador,
    ResolverManoHandler resolver,
    RedactorDeRespuesta redactor,
    MemoriaDeContexto memoria)
{
    public event EventHandler<EventoDeCopiloto>? Publicado;

    public void Conectar()
    {
        reconocedor.Reconocido += (_, dictado) => Procesar(dictado);
        reconocedor.NoReconocido += (_, crudo) => Publicar(
            new EventoDeCopiloto(crudo, "", "No te entendí.", false, null, null, null));
    }

    public EventoDeCopiloto Procesar(DictadoReconocido dictado)
    {
        memoria.Aplicar(dictado);

        var resultado = resolver.Resolver(new ConsultaDeMano(
            memoria.Situacion, memoria.StackBB, memoria.Spot,
            dictado.RangoAlto, dictado.RangoBajo, dictado.Palo));

        var evento = new EventoDeCopiloto(
            dictado.TextoCrudo,
            resultado.Respuesta?.Mano ?? "",
            redactor.Redactar(resultado),
            resultado.Respuesta is not null,
            memoria.Situacion,
            resultado.Respuesta?.ClaveDeStack,
            memoria.Spot);

        Publicar(evento);
        return evento;
    }

    private void Publicar(EventoDeCopiloto evento)
    {
        // Pausar mientras habla, o el reconocedor se escucha a sí mismo
        // y dispara una consulta fantasma con su propia respuesta.
        reconocedor.Pausar();
        try
        {
            sintetizador.Hablar(evento.Respuesta);
        }
        finally
        {
            reconocedor.Reanudar();
        }
        Publicado?.Invoke(this, evento);
    }
}
```

- [ ] **Step 6: Correr y confirmar que pasan**

```bash
dotnet test tests/PokerProOS.Tests --filter CopilotoDeVozTests
```

Esperado: 9 pruebas en verde.

- [ ] **Step 7: Correr toda la suite**

```bash
dotnet test PokerProOS.slnx
```

Esperado: todo en verde. Confirmar el total antes de seguir.

- [ ] **Step 8: Commit**

```bash
git add -A
git commit -m "feat: bucle del copiloto con memoria de contexto

Pausa el reconocedor mientras habla para que no se escuche a si mismo.
El stack y el spot persisten entre consultas, asi que no hay que
repetir el contexto cada vez."
```

---

### Task 9: API — endpoints de tablas y canal de eventos

**Files:**
- Create: `src/PokerProOS.Api/Controllers/TablasController.cs`
- Create: `src/PokerProOS.Api/Voz/CanalDeEventos.cs`
- Create: `src/PokerProOS.Api/Voz/ServicioDeCopiloto.cs`
- Create: `src/PokerProOS.Api/Controllers/VozController.cs`
- Modify: `src/PokerProOS.Api/Program.cs` (reescritura completa)
- Modify: `src/PokerProOS.Api/PokerProOS.Api.csproj` (TFM y referencia a Voz.Sapi)
- Delete: `src/PokerProOS.Api/Controllers/ChartsController.cs`, `src/PokerProOS.Api/Controllers/SessionsController.cs`, `src/PokerProOS.Api/Controllers/TrainerController.cs`
- Delete: `src/PokerProOS.Application/Charts/`, `src/PokerProOS.Application/Sessions/`, `src/PokerProOS.Application/Trainer/`
- Delete: `src/PokerProOS.Infrastructure/Repositories/`, `src/PokerProOS.Infrastructure/Services/ChartImportService.cs`

**Interfaces:**
- Consumes: todo lo anterior.
- Produces:
  - `GET /api/tablas` → catálogo completo con situaciones, stacks, spots, acciones del registro y problemas de validación.
  - `GET /api/tablas/{situacion}/{stack}/{spot}` → un spot con sus 169 celdas y conteos.
  - `GET /api/voz/eventos` → SSE con `EventoDeCopiloto` serializado.
  - `GET /api/voz/estado` → `{ escuchando, ultimaFrase, motorDisponible }`.

Los controladores viejos y sus capas se borran: `Sessions` y `Trainer` no están en el alcance y sus entidades quedan en Domain esperando a sus módulos, tal como dice el spec.

- [ ] **Step 1: Ajustar el proyecto de API**

```bash
dotnet add src/PokerProOS.Api reference src/PokerProOS.Voz.Sapi
```

Editar `src/PokerProOS.Api/PokerProOS.Api.csproj` y fijar `<TargetFramework>net10.0-windows</TargetFramework>`, porque referencia el proyecto de voz.

- [ ] **Step 2: Escribir el canal de eventos**

`src/PokerProOS.Api/Voz/CanalDeEventos.cs`:

```csharp
using System.Threading.Channels;
using PokerProOS.Application.Voz;

namespace PokerProOS.Api.Voz;

/// <summary>
/// Reparte los eventos del copiloto a cada navegador conectado por SSE.
/// Cada suscriptor tiene su propio canal acotado: si uno se atrasa, se le
/// descarta el evento más viejo en vez de frenar al resto.
/// </summary>
public sealed class CanalDeEventos
{
    private readonly List<Channel<EventoDeCopiloto>> _suscriptores = [];
    private readonly Lock _candado = new();

    public EventoDeCopiloto? Ultimo { get; private set; }

    public void Publicar(EventoDeCopiloto evento)
    {
        Ultimo = evento;
        lock (_candado)
            foreach (var canal in _suscriptores)
                canal.Writer.TryWrite(evento);
    }

    public (ChannelReader<EventoDeCopiloto> Lector, IDisposable Suscripcion) Suscribir()
    {
        var canal = Channel.CreateBounded<EventoDeCopiloto>(
            new BoundedChannelOptions(16) { FullMode = BoundedChannelFullMode.DropOldest });

        lock (_candado) _suscriptores.Add(canal);

        return (canal.Reader, new Baja(this, canal));
    }

    private sealed class Baja(CanalDeEventos canal, Channel<EventoDeCopiloto> propio) : IDisposable
    {
        public void Dispose()
        {
            lock (canal._candado) canal._suscriptores.Remove(propio);
            propio.Writer.TryComplete();
        }
    }
}
```

- [ ] **Step 3: Escribir el servicio de fondo**

`src/PokerProOS.Api/Voz/ServicioDeCopiloto.cs`:

```csharp
using PokerProOS.Application.Voz;

namespace PokerProOS.Api.Voz;

/// <summary>
/// Enciende el copiloto al arrancar la aplicación. Si el motor de voz no
/// está disponible, la aplicación sigue funcionando sin voz: las tablas
/// se consultan igual desde la pantalla.
/// </summary>
public sealed class ServicioDeCopiloto(
    CopilotoDeVoz copiloto,
    IReconocedorDeVoz reconocedor,
    CanalDeEventos canal,
    ILogger<ServicioDeCopiloto> registro) : BackgroundService
{
    public bool Escuchando { get; private set; }
    public string? Falla { get; private set; }

    protected override Task ExecuteAsync(CancellationToken cancelacion)
    {
        try
        {
            copiloto.Conectar();
            copiloto.Publicado += (_, evento) => canal.Publicar(evento);
            reconocedor.ComenzarEscuchaContinua();
            Escuchando = true;
            registro.LogInformation("Copiloto de voz escuchando.");
        }
        catch (Exception ex)
        {
            Falla = ex.Message;
            Escuchando = false;
            registro.LogError(ex, "No se pudo iniciar el copiloto de voz. La aplicación sigue sin voz.");
        }
        return Task.CompletedTask;
    }
}
```

- [ ] **Step 4: Escribir los controladores**

`src/PokerProOS.Api/Controllers/TablasController.cs`:

```csharp
using Microsoft.AspNetCore.Mvc;
using PokerProOS.Application.Tablas;

namespace PokerProOS.Api.Controllers;

[ApiController]
[Route("api/tablas")]
public sealed class TablasController(
    ICatalogoDeTablas catalogo,
    IRegistroDeAcciones acciones) : ControllerBase
{
    [HttpGet]
    public IActionResult Catalogo() => Ok(new
    {
        acciones = acciones.Todas,
        situaciones = catalogo.Situaciones.Select(s => new
        {
            s.Clave,
            s.Etiqueta,
            stacks = s.Stacks.Select(t => new
            {
                t.Stack.Clave,
                t.Stack.MinBB,
                t.Stack.MaxBB,
                spots = t.Spots.Select(p => new { p.Clave, p.Etiqueta })
            })
        }),
        problemas = catalogo.Problemas
    });

    [HttpGet("{situacion}/{stack}/{spot}")]
    public IActionResult Spot(string situacion, string stack, string spot)
    {
        var encontrado = catalogo.Spot(situacion, stack, spot);
        return encontrado is null
            ? NotFound(new { error = $"No existe el spot {spot} en {stack}." })
            : Ok(new { encontrado.Clave, encontrado.Etiqueta, encontrado.Celdas, encontrado.Conteos });
    }
}
```

`src/PokerProOS.Api/Controllers/VozController.cs`:

```csharp
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
    [HttpGet("estado")]
    public IActionResult Estado() => Ok(new
    {
        escuchando = copiloto.Escuchando,
        falla = copiloto.Falla,
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
                    await Response.WriteAsync($"data: {JsonSerializer.Serialize(evento)}\n\n", cancelacion);
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
```

- [ ] **Step 5: Reescribir `Program.cs`**

```csharp
using PokerProOS.Api.Voz;
using PokerProOS.Application.Tablas;
using PokerProOS.Application.Voz;
using PokerProOS.Infrastructure.Tablas;
using PokerProOS.Infrastructure.Voz;
using PokerProOS.Voz.Sapi;

var builder = WebApplication.CreateBuilder(args);

// Los datos viven junto al ejecutable: el csproj los copia a la salida.
// Nada de subir cinco directorios desde AppContext.BaseDirectory.
var carpetaDatos = Path.Combine(AppContext.BaseDirectory, "database");

var acciones = RegistroDeAccionesJson.Cargar(Path.Combine(carpetaDatos, "registro", "acciones.json"));
var vocabulario = RegistroDeVocabularioJson.Cargar(Path.Combine(carpetaDatos, "registro", "vocabulario.json"));
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
```

Agregar al `PokerProOS.Api.csproj` la copia de los datos a la salida, que reemplaza la ruta frágil de cinco niveles:

```xml
<ItemGroup>
  <Content Include="..\..\database\**\*.json"
           Link="database\%(RecursiveDir)%(Filename)%(Extension)"
           CopyToOutputDirectory="PreserveNewest" />
</ItemGroup>
```

- [ ] **Step 6: Borrar las capas fuera de alcance**

```bash
rm src/PokerProOS.Api/Controllers/ChartsController.cs
rm src/PokerProOS.Api/Controllers/SessionsController.cs
rm src/PokerProOS.Api/Controllers/TrainerController.cs
rm -r src/PokerProOS.Application/Charts
rm -r src/PokerProOS.Application/Sessions
rm -r src/PokerProOS.Application/Trainer
rm -r src/PokerProOS.Infrastructure/Repositories
rm src/PokerProOS.Infrastructure/Services/ChartImportService.cs
dotnet build PokerProOS.slnx
```

- [ ] **Step 7: Verificar a mano que la API responde**

```bash
dotnet run --project src/PokerProOS.Api &
sleep 8
curl -s http://localhost:5000/api/tablas | head -c 400
curl -s http://localhost:5000/api/tablas/HU_SB_OR_FISH/10bb/SB_OR | head -c 300
curl -s http://localhost:5000/api/voz/estado
```

Esperado: el catálogo trae 4 acciones, 1 situación con 11 stacks y `problemas` vacío. El spot trae 169 celdas. El estado dice `escuchando: true`. Detener el proceso al terminar.

- [ ] **Step 8: Commit**

```bash
git add -A
git commit -m "feat: API de tablas y canal de eventos de voz

Reescribe el arranque para cargar catalogo y registros sin base de
datos y sin la ruta de cinco niveles que fallaba al publicar. Elimina
los controladores y capas fuera del alcance de esta version."
```

---

### Task 10: Persistencia con migraciones y bitácora de consultas

**Files:**
- Create: `src/PokerProOS.Domain/Bitacora/ConsultaDeVoz.cs`
- Create: `src/PokerProOS.Infrastructure/Database/Configurations/ConsultaDeVozConfig.cs`
- Create: `src/PokerProOS.Infrastructure/Database/SincronizadorDeCatalogo.cs`
- Create: `src/PokerProOS.Infrastructure/Database/BitacoraDeConsultas.cs`
- Create: `src/PokerProOS.Application/Bitacora/IBitacoraDeConsultas.cs`
- Create: `tests/PokerProOS.Tests/Datos/SincronizadorTests.cs`
- Modify: `src/PokerProOS.Infrastructure/Database/PokerProOSDbContext.cs`
- Modify: `src/PokerProOS.Api/Program.cs`

**Interfaces:**
- Consumes: `ICatalogoDeTablas` (Task 4), `EventoDeCopiloto` (Task 8).
- Produces:
  - `class ConsultaDeVoz` con `int Id`, `string Situacion`, `string ClaveDeStack`, `string Spot`, `string Mano`, `string Accion`, `bool Resuelta`, `string TextoCrudo`, `DateTime CreadaEn`
  - `interface IBitacoraDeConsultas` con `Task RegistrarAsync(EventoDeCopiloto evento, CancellationToken ct)`
  - `SincronizadorDeCatalogo(PokerProOSDbContext contexto)` con `Task<int> SincronizarAsync(ICatalogoDeTablas catalogo, CancellationToken ct)`

Regla del spec: si la base no está disponible, la aplicación arranca igual. Tanto el sincronizador como la bitácora tragan el fallo de conexión y lo registran, sin propagarlo.

- [ ] **Step 1: Crear la entidad y su configuración**

`src/PokerProOS.Domain/Bitacora/ConsultaDeVoz.cs`:

```csharp
namespace PokerProOS.Domain.Bitacora;

public class ConsultaDeVoz
{
    public int Id { get; set; }
    public string Situacion { get; set; } = "";
    public string ClaveDeStack { get; set; } = "";
    public string Spot { get; set; } = "";
    public string Mano { get; set; } = "";
    public string Accion { get; set; } = "";
    public bool Resuelta { get; set; }
    public string TextoCrudo { get; set; } = "";
    public DateTime CreadaEn { get; set; } = DateTime.UtcNow;
}
```

`src/PokerProOS.Infrastructure/Database/Configurations/ConsultaDeVozConfig.cs`:

```csharp
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using PokerProOS.Domain.Bitacora;

namespace PokerProOS.Infrastructure.Database.Configurations;

public class ConsultaDeVozConfig : IEntityTypeConfiguration<ConsultaDeVoz>
{
    public void Configure(EntityTypeBuilder<ConsultaDeVoz> constructor)
    {
        constructor.HasKey(e => e.Id);
        constructor.Property(e => e.Situacion).HasMaxLength(50).IsRequired();
        constructor.Property(e => e.ClaveDeStack).HasMaxLength(20).IsRequired();
        constructor.Property(e => e.Spot).HasMaxLength(50).IsRequired();
        constructor.Property(e => e.Mano).HasMaxLength(10).IsRequired();
        constructor.Property(e => e.Accion).HasMaxLength(20).IsRequired();
        constructor.Property(e => e.TextoCrudo).HasMaxLength(500).IsRequired();
        // El indice sirve la pregunta que motiva la bitacora:
        // que manos consulto mas en cada spot.
        constructor.HasIndex(e => new { e.Situacion, e.ClaveDeStack, e.Spot, e.Mano });
    }
}
```

Agregar el `DbSet` al contexto:

```csharp
public DbSet<ConsultaDeVoz> ConsultasDeVoz => Set<ConsultaDeVoz>();
```

- [ ] **Step 2: Reemplazar `EnsureCreated` por migraciones**

```bash
dotnet tool install --global dotnet-ef --version 10.0.0 2>/dev/null || dotnet tool update --global dotnet-ef --version 10.0.0
dotnet ef migrations add InicialConBitacora \
  --project src/PokerProOS.Infrastructure \
  --startup-project src/PokerProOS.Api \
  --output-dir Database/Migraciones
```

Si la base `PokerProOS` ya existe creada por el `EnsureCreated` anterior, hay que tirarla una única vez para que la migración inicial pueda aplicarse limpia:

```bash
dotnet ef database drop --force \
  --project src/PokerProOS.Infrastructure --startup-project src/PokerProOS.Api
```

Es la última vez que hace falta borrar la base: a partir de acá el esquema evoluciona con migraciones.

- [ ] **Step 3: Escribir las pruebas del sincronizador**

```csharp
using Microsoft.EntityFrameworkCore;
using PokerProOS.Infrastructure.Database;
using PokerProOS.Infrastructure.Tablas;

namespace PokerProOS.Tests.Datos;

public class SincronizadorTests
{
    private static PokerProOSDbContext ContextoEnMemoria() =>
        new(new DbContextOptionsBuilder<PokerProOSDbContext>()
            .UseInMemoryDatabase($"prueba-{Guid.NewGuid():N}").Options);

    private static ICatalogoDeTablas Catalogo() =>
        new CargadorDeTablas(new ValidadorDeTabla(
                RegistroDeAccionesJson.Cargar(Rutas.Registro("acciones.json"))))
            .CargarDirectorio(Rutas.SemillasDeTablas);

    [Fact]
    public async Task Sincroniza_todas_las_celdas_del_catalogo()
    {
        using var contexto = ContextoEnMemoria();
        var escritas = await new SincronizadorDeCatalogo(contexto)
            .SincronizarAsync(Catalogo(), TestContext.Current.CancellationToken);

        // 11 stacks: dos con 3 spots y nueve con 5, por 169 manos.
        Assert.Equal((2 * 3 + 9 * 5) * 169, escritas);
        Assert.Equal(escritas, await contexto.ChartStrategyCells.CountAsync(
            TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task Sincronizar_dos_veces_no_duplica_filas()
    {
        using var contexto = ContextoEnMemoria();
        var sincronizador = new SincronizadorDeCatalogo(contexto);
        var catalogo = Catalogo();

        await sincronizador.SincronizarAsync(catalogo, TestContext.Current.CancellationToken);
        var primera = await contexto.ChartStrategyCells.CountAsync(TestContext.Current.CancellationToken);
        await sincronizador.SincronizarAsync(catalogo, TestContext.Current.CancellationToken);
        var segunda = await contexto.ChartStrategyCells.CountAsync(TestContext.Current.CancellationToken);

        Assert.Equal(primera, segunda);
    }
}
```

Agregar el paquete del proveedor en memoria:

```bash
dotnet add tests/PokerProOS.Tests package Microsoft.EntityFrameworkCore.InMemory --version 10.0.0
```

- [ ] **Step 4: Correr y confirmar que fallan**

```bash
dotnet test tests/PokerProOS.Tests --filter SincronizadorTests
```

- [ ] **Step 5: Implementar sincronizador y bitácora**

`src/PokerProOS.Infrastructure/Database/SincronizadorDeCatalogo.cs`:

```csharp
using Microsoft.EntityFrameworkCore;
using PokerProOS.Application.Tablas;
using PokerProOS.Domain.Entities;
using PokerProOS.Infrastructure.Database;

namespace PokerProOS.Infrastructure.Database;

/// <summary>
/// Vuelca el catálogo validado a la base. Los JSON son la fuente de verdad;
/// esto es solo el espejo consultable para cruces relacionales.
/// </summary>
public sealed class SincronizadorDeCatalogo(PokerProOSDbContext contexto)
{
    public async Task<int> SincronizarAsync(ICatalogoDeTablas catalogo, CancellationToken cancelacion)
    {
        var celdas = new List<ChartStrategyCell>();

        foreach (var situacion in catalogo.Situaciones)
            foreach (var tabla in situacion.Stacks)
                foreach (var spot in tabla.Spots)
                    foreach (var celda in spot.Celdas)
                        celdas.Add(new ChartStrategyCell
                        {
                            SituationKey = situacion.Clave,
                            SituationLabel = situacion.Etiqueta,
                            StackKey = tabla.Stack.Clave,
                            MinBB = tabla.Stack.MinBB,
                            MaxBB = tabla.Stack.MaxBB,
                            SpotKey = spot.Clave,
                            SpotLabel = spot.Etiqueta,
                            HandLabel = celda.Mano,
                            Action = celda.Accion,
                            Source = "json",
                            Version = "v1",
                            UpdatedAt = DateTime.UtcNow
                        });

        // Reemplazo completo: los JSON mandan, lo que haya en la base sobra.
        await contexto.ChartStrategyCells.ExecuteDeleteAsync(cancelacion);
        contexto.ChartStrategyCells.AddRange(celdas);
        await contexto.SaveChangesAsync(cancelacion);
        return celdas.Count;
    }
}
```

Nota: `ExecuteDeleteAsync` no está soportado por el proveedor en memoria. En las pruebas hay que usar `contexto.ChartStrategyCells.RemoveRange(contexto.ChartStrategyCells)` cuando el proveedor no es relacional. Implementarlo así:

```csharp
if (contexto.Database.IsRelational())
    await contexto.ChartStrategyCells.ExecuteDeleteAsync(cancelacion);
else
    contexto.ChartStrategyCells.RemoveRange(contexto.ChartStrategyCells);
```

`src/PokerProOS.Application/Bitacora/IBitacoraDeConsultas.cs`:

```csharp
using PokerProOS.Application.Voz;

namespace PokerProOS.Application.Bitacora;

public interface IBitacoraDeConsultas
{
    Task RegistrarAsync(EventoDeCopiloto evento, CancellationToken cancelacion);
}
```

`src/PokerProOS.Infrastructure/Database/BitacoraDeConsultas.cs`:

```csharp
using Microsoft.Extensions.Logging;
using PokerProOS.Application.Bitacora;
using PokerProOS.Application.Voz;
using PokerProOS.Domain.Bitacora;
using PokerProOS.Infrastructure.Database;

namespace PokerProOS.Infrastructure.Database;

public sealed class BitacoraDeConsultas(
    PokerProOSDbContext contexto,
    ILogger<BitacoraDeConsultas> registro) : IBitacoraDeConsultas
{
    public async Task RegistrarAsync(EventoDeCopiloto evento, CancellationToken cancelacion)
    {
        try
        {
            contexto.ConsultasDeVoz.Add(new ConsultaDeVoz
            {
                Situacion = evento.Situacion ?? "",
                ClaveDeStack = evento.ClaveDeStack ?? "",
                Spot = evento.Spot ?? "",
                Mano = evento.ManoInterpretada,
                Accion = evento.Respuesta,
                Resuelta = evento.Resuelta,
                TextoCrudo = evento.TextoCrudo,
                CreadaEn = DateTime.UtcNow
            });
            await contexto.SaveChangesAsync(cancelacion);
        }
        catch (Exception ex)
        {
            // La herramienta de estudio no se cae porque la base no este.
            registro.LogWarning(ex, "No se pudo registrar la consulta en la bitácora.");
        }
    }
}
```

- [ ] **Step 6: Enganchar en `Program.cs`**

Insertar antes de `app.Run()`:

```csharp
builder.Services.AddDbContext<PokerProOSDbContext>(opciones =>
    opciones.UseSqlServer(builder.Configuration.GetConnectionString("DefaultConnection")));
builder.Services.AddScoped<IBitacoraDeConsultas, BitacoraDeConsultas>();
```

Y después de construir la aplicación:

```csharp
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
```

Y suscribir la bitácora al canal, dentro de `ServicioDeCopiloto.ExecuteAsync`, usando `IServiceScopeFactory` para obtener un `IBitacoraDeConsultas` por evento, porque el contexto es `Scoped` y el servicio es `Singleton`.

- [ ] **Step 7: Correr toda la suite y verificar a mano**

```bash
dotnet test PokerProOS.slnx
dotnet run --project src/PokerProOS.Api &
sleep 10
curl -s http://localhost:5000/api/tablas | head -c 200
```

Esperado: el log dice "Catálogo sincronizado: 8619 celdas." Detener el servicio, detener SQL Server con `net stop MSSQLSERVER`, arrancar de nuevo y confirmar que la aplicación levanta igual con la advertencia de que no hay historial. Volver a arrancar SQL Server al terminar.

- [ ] **Step 8: Commit**

```bash
git add -A
git commit -m "feat: migraciones EF y bitacora de consultas de voz

Reemplaza EnsureCreated por migraciones para que el esquema evolucione
sin perder historial. Sincroniza el catalogo a la base como espejo
consultable y registra cada consulta de voz. Si la base no esta, la
aplicacion arranca igual sin historial."
```

---

### Task 11: Interfaz — colores del registro, selectores y estado de voz

**Files:**
- Create: `frontend/src/core/models/catalogo.model.ts`
- Create: `frontend/src/core/services/tablasApi.ts`
- Create: `frontend/src/core/hooks/useCatalogo.ts`
- Create: `frontend/src/core/hooks/useEventosDeVoz.ts`
- Create: `frontend/src/features/tablas/PaginaDeTablas.tsx`
- Create: `frontend/src/features/tablas/Grilla.tsx`
- Create: `frontend/src/features/tablas/Celda.tsx`
- Create: `frontend/src/features/tablas/Selectores.tsx`
- Create: `frontend/src/features/tablas/Leyenda.tsx`
- Create: `frontend/src/features/tablas/EstadoDeVoz.tsx`
- Create: `frontend/src/features/tablas/AvisoDeProblemas.tsx`
- Modify: `frontend/src/App.tsx`, `frontend/src/index.css`, `frontend/vite.config.ts`
- Delete: `frontend/src/core/constants/poker.ts`, `frontend/src/core/models/chart.model.ts`, `frontend/src/core/services/chartApi.ts`, `frontend/src/core/hooks/useChart.ts`, `frontend/src/features/spins/`

**Interfaces:**
- Consumes: `GET /api/tablas`, `GET /api/tablas/{situacion}/{stack}/{spot}`, `GET /api/voz/eventos`, `GET /api/voz/estado` (Task 9).
- Produces: la aplicación completa servida desde `wwwroot`.

El archivo `frontend/src/core/constants/poker.ts` desaparece: es el que hardcodeaba los once stacks y los colores invertidos. Todo eso llega del catálogo.

- [ ] **Step 1: Agregar el proxy de desarrollo**

Hoy `vite.config.ts` no tiene proxy, así que `npm run dev` no puede llegar a la API. Editar:

```ts
import react from '@vitejs/plugin-react'
import { defineConfig } from 'vite'

export default defineConfig({
  plugins: [react()],
  server: {
    proxy: {
      '/api': {
        target: 'http://localhost:5000',
        changeOrigin: true,
      },
    },
  },
})
```

- [ ] **Step 2: Definir los modelos y el cliente**

`frontend/src/core/models/catalogo.model.ts`:

```ts
export interface AccionDefinida {
  clave: string
  etiqueta: string
  color: string
  colorTexto: string
  orden: number
  dichos: string[]
}

export interface SpotResumen {
  clave: string
  etiqueta: string
}

export interface StackResumen {
  clave: string
  minBB: number
  maxBB: number
  spots: SpotResumen[]
}

export interface SituacionResumen {
  clave: string
  etiqueta: string
  stacks: StackResumen[]
}

export interface ProblemaDeTabla {
  archivo: string
  stack: string
  spot: string
  mensaje: string
}

export interface Catalogo {
  acciones: AccionDefinida[]
  situaciones: SituacionResumen[]
  problemas: ProblemaDeTabla[]
}

export interface Celda {
  mano: string
  accion: string
}

export interface SpotCompleto {
  clave: string
  etiqueta: string
  celdas: Celda[]
  conteos: Record<string, number>
}

export interface EventoDeVoz {
  textoCrudo: string
  manoInterpretada: string
  respuesta: string
  resuelta: boolean
  situacion: string | null
  claveDeStack: string | null
  spot: string | null
}
```

`frontend/src/core/services/tablasApi.ts`:

```ts
import type { Catalogo, SpotCompleto } from '../models/catalogo.model'

async function pedir<T>(url: string): Promise<T> {
  const respuesta = await fetch(url)
  if (!respuesta.ok) throw new Error(`${respuesta.status} ${respuesta.statusText}`)
  return respuesta.json() as Promise<T>
}

export const obtenerCatalogo = () => pedir<Catalogo>('/api/tablas')

export const obtenerSpot = (situacion: string, stack: string, spot: string) =>
  pedir<SpotCompleto>(`/api/tablas/${situacion}/${stack}/${spot}`)

export const obtenerEstadoDeVoz = () =>
  pedir<{ escuchando: boolean; falla: string | null; ultimaFrase: string | null }>('/api/voz/estado')
```

- [ ] **Step 3: Escribir los hooks**

`frontend/src/core/hooks/useCatalogo.ts`:

```ts
import { useEffect, useState } from 'react'
import type { Catalogo } from '../models/catalogo.model'
import { obtenerCatalogo } from '../services/tablasApi'

export function useCatalogo() {
  const [catalogo, setCatalogo] = useState<Catalogo | null>(null)
  const [error, setError] = useState<string | null>(null)

  useEffect(() => {
    let cancelado = false
    obtenerCatalogo()
      .then((datos) => { if (!cancelado) setCatalogo(datos) })
      .catch((e: unknown) => {
        if (!cancelado) setError(e instanceof Error ? e.message : 'Error desconocido')
      })
    return () => { cancelado = true }
  }, [])

  return { catalogo, error }
}
```

`frontend/src/core/hooks/useEventosDeVoz.ts`:

```ts
import { useEffect, useState } from 'react'
import type { EventoDeVoz } from '../models/catalogo.model'

/**
 * Se suscribe al canal SSE del copiloto. EventSource reconecta solo,
 * asi que no hace falta watchdog del lado del navegador.
 */
export function useEventosDeVoz() {
  const [ultimo, setUltimo] = useState<EventoDeVoz | null>(null)
  const [conectado, setConectado] = useState(false)

  useEffect(() => {
    const fuente = new EventSource('/api/voz/eventos')
    fuente.onopen = () => setConectado(true)
    fuente.onerror = () => setConectado(false)
    fuente.onmessage = (mensaje) => setUltimo(JSON.parse(mensaje.data) as EventoDeVoz)
    return () => fuente.close()
  }, [])

  return { ultimo, conectado }
}
```

- [ ] **Step 4: Escribir los componentes**

`frontend/src/features/tablas/Celda.tsx`:

```tsx
import type { AccionDefinida } from '../../core/models/catalogo.model'

interface Props {
  mano: string
  accion: AccionDefinida | undefined
  resaltada: boolean
}

export function Celda({ mano, accion, resaltada }: Props) {
  return (
    <div
      className={`celda${resaltada ? ' celda-resaltada' : ''}`}
      style={{
        backgroundColor: accion?.color ?? '#374151',
        color: accion?.colorTexto ?? '#edf3fb',
      }}
      title={`${mano}: ${accion?.etiqueta ?? 'desconocida'}`}
    >
      {/* El color nunca es la unica senal: la etiqueta va siempre visible. */}
      {mano}
    </div>
  )
}
```

`frontend/src/features/tablas/Grilla.tsx`:

```tsx
import type { AccionDefinida, SpotCompleto } from '../../core/models/catalogo.model'
import { Celda } from './Celda'

const RANGOS = ['A', 'K', 'Q', 'J', 'T', '9', '8', '7', '6', '5', '4', '3', '2']

interface Props {
  spot: SpotCompleto
  acciones: AccionDefinida[]
  manoResaltada: string | null
}

function etiqueta(fila: number, columna: number): string {
  const alto = RANGOS[Math.min(fila, columna)]
  const bajo = RANGOS[Math.max(fila, columna)]
  if (fila === columna) return `${alto}${bajo}`
  return fila < columna ? `${alto}${bajo}s` : `${alto}${bajo}o`
}

export function Grilla({ spot, acciones, manoResaltada }: Props) {
  const porMano = new Map(spot.celdas.map((c) => [c.mano, c.accion]))
  const porClave = new Map(acciones.map((a) => [a.clave, a]))

  return (
    <div className="grilla">
      <div className="grilla-fila">
        <div className="grilla-esquina" />
        {RANGOS.map((r) => <div key={r} className="grilla-encabezado">{r}</div>)}
      </div>
      {RANGOS.map((rangoFila, fila) => (
        <div key={rangoFila} className="grilla-fila">
          <div className="grilla-encabezado">{rangoFila}</div>
          {RANGOS.map((_, columna) => {
            const mano = etiqueta(fila, columna)
            return (
              <Celda
                key={mano}
                mano={mano}
                accion={porClave.get(porMano.get(mano) ?? '')}
                resaltada={mano === manoResaltada}
              />
            )
          })}
        </div>
      ))}
    </div>
  )
}
```

`frontend/src/features/tablas/EstadoDeVoz.tsx`:

```tsx
interface Props {
  conectado: boolean
  ultimaFrase: string | null
  manoInterpretada: string | null
  respuesta: string | null
}

export function EstadoDeVoz({ conectado, ultimaFrase, manoInterpretada, respuesta }: Props) {
  return (
    <section className="estado-voz" aria-live="polite">
      <span className={`indicador${conectado ? ' indicador-activo' : ''}`}>
        {conectado ? 'Escuchando' : 'Sin voz'}
      </span>
      {/* Ver lo que escucho es lo que permite detectar que entendio mal. */}
      {ultimaFrase && <span className="frase-cruda">«{ultimaFrase}»</span>}
      {manoInterpretada && <strong className="mano-interpretada">{manoInterpretada}</strong>}
      {respuesta && <span className="respuesta">{respuesta}</span>}
    </section>
  )
}
```

`frontend/src/features/tablas/Leyenda.tsx`:

```tsx
import type { AccionDefinida } from '../../core/models/catalogo.model'

export function Leyenda({ acciones, conteos }: {
  acciones: AccionDefinida[]
  conteos: Record<string, number>
}) {
  return (
    <div className="leyenda">
      {/* Se arma sola con lo que declare el registro. */}
      {acciones.map((accion) => (
        <span key={accion.clave} className="leyenda-item">
          <i style={{ backgroundColor: accion.color }} />
          {accion.etiqueta}
          <b>{conteos[accion.clave] ?? 0}</b>
        </span>
      ))}
    </div>
  )
}
```

`frontend/src/features/tablas/AvisoDeProblemas.tsx`:

```tsx
import type { ProblemaDeTabla } from '../../core/models/catalogo.model'

export function AvisoDeProblemas({ problemas }: { problemas: ProblemaDeTabla[] }) {
  if (problemas.length === 0) return null
  return (
    <section className="aviso-problemas">
      <strong>{problemas.length} tabla(s) con problemas. El resto se cargó igual.</strong>
      <ul>
        {problemas.map((p, i) => (
          <li key={i}>
            <code>{p.archivo}</code> {p.stack}/{p.spot}: {p.mensaje}
          </li>
        ))}
      </ul>
    </section>
  )
}
```

`frontend/src/features/tablas/Selectores.tsx`:

```tsx
import type { SituacionResumen } from '../../core/models/catalogo.model'

interface Props {
  situaciones: SituacionResumen[]
  situacion: string
  stack: string
  spot: string
  onSituacion: (clave: string) => void
  onStack: (clave: string) => void
  onSpot: (clave: string) => void
}

export function Selectores({
  situaciones, situacion, stack, spot, onSituacion, onStack, onSpot,
}: Props) {
  // Todas las opciones salen del catalogo. No hay listas en el front.
  const situacionActiva = situaciones.find((s) => s.clave === situacion)
  const stackActivo = situacionActiva?.stacks.find((t) => t.clave === stack)

  return (
    <div className="selectores">
      <label>
        Situación
        <select value={situacion} onChange={(e) => onSituacion(e.target.value)}>
          {situaciones.map((s) => (
            <option key={s.clave} value={s.clave}>{s.etiqueta}</option>
          ))}
        </select>
      </label>
      <label>
        Stack
        <select value={stack} onChange={(e) => onStack(e.target.value)}>
          {situacionActiva?.stacks.map((t) => (
            <option key={t.clave} value={t.clave}>{t.clave}</option>
          ))}
        </select>
      </label>
      <label>
        Spot
        <select value={spot} onChange={(e) => onSpot(e.target.value)}>
          {stackActivo?.spots.map((p) => (
            <option key={p.clave} value={p.clave}>{p.etiqueta}</option>
          ))}
        </select>
      </label>
    </div>
  )
}
```

`frontend/src/features/tablas/PaginaDeTablas.tsx`. Acá vive la sincronización entre la voz y la pantalla, que es la parte que cierra el bucle:

```tsx
import { useEffect, useState } from 'react'
import { useCatalogo } from '../../core/hooks/useCatalogo'
import { useEventosDeVoz } from '../../core/hooks/useEventosDeVoz'
import type { SpotCompleto } from '../../core/models/catalogo.model'
import { obtenerSpot } from '../../core/services/tablasApi'
import { AvisoDeProblemas } from './AvisoDeProblemas'
import { EstadoDeVoz } from './EstadoDeVoz'
import { Grilla } from './Grilla'
import { Leyenda } from './Leyenda'
import { Selectores } from './Selectores'

export function PaginaDeTablas() {
  const { catalogo, error } = useCatalogo()
  const { ultimo, conectado } = useEventosDeVoz()

  const [situacion, setSituacion] = useState('')
  const [stack, setStack] = useState('')
  const [spot, setSpot] = useState('')
  const [datos, setDatos] = useState<SpotCompleto | null>(null)

  // Seleccion inicial: la primera de cada nivel, tomada del catalogo.
  useEffect(() => {
    if (!catalogo || situacion) return
    const primera = catalogo.situaciones[0]
    if (!primera) return
    const primerStack = primera.stacks[0]
    setSituacion(primera.clave)
    setStack(primerStack?.clave ?? '')
    setSpot(primerStack?.spots[0]?.clave ?? '')
  }, [catalogo, situacion])

  // La voz manda sobre los selectores: si el dictado trajo stack o spot,
  // la pantalla se mueve a la tabla que se acaba de consultar.
  useEffect(() => {
    if (!ultimo?.resuelta) return
    if (ultimo.claveDeStack) setStack(ultimo.claveDeStack)
    if (ultimo.spot) setSpot(ultimo.spot)
    if (ultimo.situacion) setSituacion(ultimo.situacion)
  }, [ultimo])

  // Al cambiar de stack, el spot activo puede no existir ahi (los stacks
  // chicos tienen 3 spots y los demas 5). Caer al primero disponible.
  useEffect(() => {
    if (!catalogo || !situacion || !stack) return
    const stackActivo = catalogo.situaciones
      .find((s) => s.clave === situacion)?.stacks
      .find((t) => t.clave === stack)
    if (stackActivo && !stackActivo.spots.some((p) => p.clave === spot))
      setSpot(stackActivo.spots[0]?.clave ?? '')
  }, [catalogo, situacion, stack, spot])

  useEffect(() => {
    if (!situacion || !stack || !spot) return
    let cancelado = false
    obtenerSpot(situacion, stack, spot)
      .then((d) => { if (!cancelado) setDatos(d) })
      .catch(() => { if (!cancelado) setDatos(null) })
    return () => { cancelado = true }
  }, [situacion, stack, spot])

  if (error) return <p className="error">No pude cargar el catálogo: {error}</p>
  if (!catalogo) return <p>Cargando…</p>

  return (
    <main className="pagina">
      <h1>Tablas preflop</h1>

      <EstadoDeVoz
        conectado={conectado}
        ultimaFrase={ultimo?.textoCrudo ?? null}
        manoInterpretada={ultimo?.manoInterpretada || null}
        respuesta={ultimo?.respuesta ?? null}
      />

      <AvisoDeProblemas problemas={catalogo.problemas} />

      <Selectores
        situaciones={catalogo.situaciones}
        situacion={situacion}
        stack={stack}
        spot={spot}
        onSituacion={setSituacion}
        onStack={setStack}
        onSpot={setSpot}
      />

      {datos && (
        <>
          <Grilla
            spot={datos}
            acciones={catalogo.acciones}
            manoResaltada={ultimo?.manoInterpretada || null}
          />
          <Leyenda acciones={catalogo.acciones} conteos={datos.conteos} />
        </>
      )}
    </main>
  )
}
```

Y `frontend/src/App.tsx` queda reducido a montarla:

```tsx
import { PaginaDeTablas } from './features/tablas/PaginaDeTablas'

export default function App() {
  return <PaginaDeTablas />
}
```

Agregar a `frontend/src/index.css` los estilos de los selectores:

```css
.pagina { padding: 20px; }
.selectores { display: flex; gap: 14px; margin-bottom: 16px; flex-wrap: wrap; }
.selectores label {
  display: grid; gap: 5px;
  color: var(--apagado); font-size: 12px; font-weight: 700;
  text-transform: uppercase; letter-spacing: .06em;
}
.selectores select {
  background: var(--panel-2); color: var(--texto);
  border: 1px solid var(--borde); border-radius: 7px;
  padding: 8px 10px; font-size: 14px; font-weight: 600;
}
.error { color: #ff6868; }
```

- [ ] **Step 5: Escribir los estilos**

`frontend/src/index.css`. Paleta del proyecto original, sobria:

```css
:root {
  --fondo: #0d1117;
  --panel: #151a21;
  --panel-2: #1b222b;
  --borde: #3a4350;
  --texto: #edf3fb;
  --apagado: #b0bac7;
  --acento: #8bb8e8;
}

* { box-sizing: border-box; }

body {
  margin: 0;
  background: var(--fondo);
  color: var(--texto);
  font-family: Inter, "Segoe UI", Arial, sans-serif;
}

.grilla { display: grid; gap: 3px; width: max-content; }
.grilla-fila { display: grid; grid-template-columns: 28px repeat(13, 40px); gap: 3px; }
.grilla-encabezado {
  display: grid; place-items: center;
  color: var(--apagado); font-size: 12px; font-weight: 700;
}
.celda {
  height: 40px; display: grid; place-items: center;
  border-radius: 3px; font-size: 11px; font-weight: 800;
  border: 1px solid transparent;
}
.celda-resaltada {
  outline: 3px solid var(--texto);
  outline-offset: 1px;
  transform: scale(1.12);
  z-index: 2;
}

.leyenda { display: flex; gap: 14px; align-items: center; margin-top: 14px; }
.leyenda-item { display: inline-flex; gap: 6px; align-items: center; font-weight: 700; font-size: 13px; }
.leyenda-item i { width: 14px; height: 14px; border-radius: 3px; }
.leyenda-item b { color: var(--apagado); font-weight: 700; }

.estado-voz {
  display: flex; gap: 12px; align-items: center; flex-wrap: wrap;
  padding: 10px 12px; margin-bottom: 14px;
  background: var(--panel); border: 1px solid var(--borde); border-radius: 8px;
}
.indicador {
  padding: 4px 10px; border-radius: 999px;
  background: var(--panel-2); color: var(--apagado);
  font-size: 12px; font-weight: 800;
}
.indicador-activo { background: #123629; color: #9ff0c8; }
.frase-cruda { color: var(--apagado); font-style: italic; }
.mano-interpretada { font-size: 18px; letter-spacing: .04em; }

.aviso-problemas {
  padding: 12px; margin-bottom: 14px;
  border: 1px solid #7a4a2a; border-radius: 8px; background: #2a1b12;
}
.aviso-problemas ul { margin: 8px 0 0; padding-left: 18px; color: var(--apagado); }
```

- [ ] **Step 6: Borrar lo hardcodeado y compilar**

```bash
cd frontend
rm src/core/constants/poker.ts
rm src/core/models/chart.model.ts
rm src/core/services/chartApi.ts
rm src/core/hooks/useChart.ts
rm -r src/features/spins
npm run lint
npm run build
```

Esperado: compila sin errores. Si `tsc` se queja de imports huérfanos en `App.tsx`, apuntarlo a `PaginaDeTablas`.

- [ ] **Step 7: Publicar en `wwwroot` y verificar a mano**

```bash
cd ..
rm -rf src/PokerProOS.Api/wwwroot/assets
cp -r frontend/dist/* src/PokerProOS.Api/wwwroot/
dotnet run --project src/PokerProOS.Api
```

Abrir `http://localhost:5000`. Verificar en persona:

1. La grilla muestra ALL-IN en verde, CALL en ámbar y FOLD en blanco. Si algo se ve azul o rojo, el registro no se está aplicando.
2. Los selectores listan los 11 stacks sin que estén escritos en el front.
3. El indicador dice "Escuchando".
4. Decir «siete be be a cinco offsuit» en voz alta: se escucha la respuesta, la frase aparece en pantalla y la celda `A5o` queda resaltada.
5. Decir «a rey» sin palo: la respuesta repite «A K offsuit» antes de la acción.

- [ ] **Step 8: Commit**

```bash
git add -A
git commit -m "feat: interfaz de tablas con colores del registro y estado de voz

Elimina la lista de stacks y el mapa de colores hardcodeados: todo
sale del catalogo y del registro. Restaura la paleta del proyecto
original y resalta la mano dictada por voz."
```

---

### Task 12: Automatizar la publicación del front y actualizar la documentación

**Files:**
- Modify: `src/PokerProOS.Api/PokerProOS.Api.csproj`
- Modify: `CLAUDE.md`
- Create: `README.md`

**Interfaces:**
- Consumes: todo lo anterior.
- Produces: `dotnet build` construye y copia el front solo.

- [ ] **Step 1: Agregar el objetivo de MSBuild**

En `src/PokerProOS.Api/PokerProOS.Api.csproj`, antes de `</Project>`. Reemplaza la copia manual que hoy no automatiza nada:

```xml
<Target Name="ConstruirFrontend" BeforeTargets="Build"
        Condition="'$(SaltearFrontend)' != 'true'">
  <Exec Command="npm install" WorkingDirectory="../../frontend"
        Condition="!Exists('../../frontend/node_modules')" />
  <Exec Command="npm run build" WorkingDirectory="../../frontend" />
  <ItemGroup>
    <ArchivosDelFrontend Include="../../frontend/dist/**/*" />
  </ItemGroup>
  <RemoveDir Directories="wwwroot" />
  <Copy SourceFiles="@(ArchivosDelFrontend)"
        DestinationFiles="@(ArchivosDelFrontend->'wwwroot/%(RecursiveDir)%(Filename)%(Extension)')" />
</Target>
```

La condición `SaltearFrontend` permite `dotnet build -p:SaltearFrontend=true` cuando solo se toca C#, para no pagar el build de Vite en cada iteración.

- [ ] **Step 2: Verificar que funciona desde cero**

```bash
rm -rf src/PokerProOS.Api/wwwroot
dotnet build src/PokerProOS.Api
ls src/PokerProOS.Api/wwwroot
```

Esperado: `index.html` y `assets/` reaparecen sin copiarlos a mano.

- [ ] **Step 3: Reescribir `CLAUDE.md`**

El archivo actual describe el proyecto anterior a este trabajo y quedó obsoleto casi entero. Reemplazar las secciones de comandos y arquitectura por el estado real: catálogo en memoria como camino de lectura, JSON como fuente de verdad, registros en datos, voz SAPI en `PokerProOS.Voz.Sapi`, migraciones EF, y el hecho de que `dotnet build` ya construye el front. Eliminar todas las advertencias que dejaron de aplicar: la ruta de cinco niveles, `EnsureCreated`, la copia manual a `wwwroot`, los enums muertos, el `ChartValidator` cascarón y `EvaluateAnswerHandler`.

Agregar la sección que más valor tiene para el futuro:

```markdown
## Agregar una tabla nueva

1. Dejar el archivo JSON en `database/seed-data/`.
2. Si usa una acción que no existe, agregarla a `database/registro/acciones.json`.
3. Arrancar. La app valida, carga, sincroniza y arma la gramática de voz sola.

No hay que tocar código. Si algo falla, la app lo dice al arrancar y en pantalla,
indicando archivo, stack, spot y causa.
```

- [ ] **Step 4: Escribir el `README.md`**

Corto: qué es, cómo se corre (`dotnet run --project src/PokerProOS.Api`), qué necesita (Windows con el reconocedor es-ES, SQL Server opcional), y cómo se dicta una consulta con tres ejemplos reales.

- [ ] **Step 5: Correr la verificación completa**

```bash
dotnet test PokerProOS.slnx
dotnet build PokerProOS.slnx
```

Esperado: toda la suite en verde y el build limpio. Anotar el número de pruebas.

- [ ] **Step 6: Commit**

```bash
git add -A
git commit -m "chore: automatiza el build del front y actualiza la documentacion

MSBuild construye y copia el frontend, que hasta ahora era una copia
manual sin nada que la automatizara. CLAUDE.md pasa a describir el
proyecto real y documenta como agregar una tabla sin tocar codigo."
```

---

## Verificación final

Antes de dar el trabajo por terminado, confirmar con evidencia:

- [ ] `dotnet test PokerProOS.slnx` en verde, con el conteo de pruebas anotado.
- [ ] `dotnet build PokerProOS.slnx` sin advertencias `CA1416`.
- [ ] Las once tablas cargan sin problemas: `curl -s localhost:5000/api/tablas` devuelve `problemas: []`.
- [ ] La grilla muestra ALL-IN verde, CALL ámbar, RAISE_X2 violeta y FOLD blanco.
- [ ] Dictar «siete be be a cinco offsuit» produce respuesta hablada y celda resaltada.
- [ ] Dictar «a rey» produce una respuesta que repite «A K offsuit».
- [ ] Con SQL Server detenido la aplicación arranca igual y las tablas funcionan.
- [ ] **La prueba del principio rector:** agregar a mano una acción `LIMP` al registro y usarla en una tabla de prueba; debe aparecer coloreada en la grilla, en la leyenda y ser dictable, sin recompilar nada más que el arranque. Revertir el cambio después.
