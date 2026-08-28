# Entrenador de tablas — Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Que la app pregunte manos en vez de solo responderlas, con repetición espaciada por casilla y una explicación en el momento de fallar.

**Architecture:** Un slice nuevo `Entrenador/` en cada capa, siguiendo la organización existente (`Tablas/`, `Voz/`, `Bitacora/`). Las dos piezas con lógica de verdad —`CalendarioDeRepeticion` y `PlanificadorDeTanda`— quedan puras: sin base, sin HTTP y sin reloj propio (la fecha entra como parámetro). El progreso va a SQL Server con `UsuarioId` en la clave desde el primer día; la respuesta correcta la resuelve `ResolverManoHandler`, el mismo que contesta por voz.

**Tech Stack:** .NET 10 (Domain ← Application ← Infrastructure ← Api), EF Core + SQL Server, xUnit, React 19 + TypeScript + Vite, oxlint.

**Spec:** `docs/superpowers/specs/2026-08-27-entrenador-design.md`

## Global Constraints

Copiadas del spec y de `CLAUDE.md`. Aplican a **todas** las tareas.

- **Nada de listas en código.** Acciones, colores, stacks, spots, situaciones y formas habladas salen de `database/registro/` o `database/seed-data/`. Las únicas constantes desnudas permitidas son los 13 rangos (`A K Q J T 9 8 7 6 5 4 3 2`) y el `169`.
- **Los botones de acción llevan el color del registro.** Si en la grilla `ALL-IN` es verde, el botón es verde. Los atajos de teclado salen del campo `orden` de `acciones.json`.
- **Dirección de dependencias estricta:** `Domain ← Application ← Infrastructure ← Api`. `Application` no conoce EF ni HTTP.
- **Sin MediatR.** Los handlers son clases planas registradas a mano en `Program.cs`.
- **`UsuarioId` es parte de la clave del progreso desde el día uno**, pero no se construye login: la API lo resuelve con la constante `UsuarioId = 1` en **un solo lugar** (`EntrenadorController.UsuarioActual`).
- **El entrenador requiere base de datos** y lo dice en pantalla en vez de fallar callado. Tablas y voz siguen andando sin SQL Server, como hasta ahora.
- **Las manos mixtas cuentan por cualquiera de sus partes.** Si `AA` es `CALL 50 / RAISE_X2 50`, responder cualquiera de las dos es acertar.
- **Nombres en español**, igual que todo el proyecto (clases, métodos, componentes, props).
- **Compilar sin la app corriendo.** `PokerProOS.Api.exe` bloquea los DLL; si el build falla con `MSB3027`, hay que apagarla primero.
- **`dotnet test PokerProOS.slnx -p:SaltearFrontend=true`** para no pagar el build de Vite en cada iteración.
- Al terminar cada tarea, **la suite entera tiene que quedar verde** (243 pruebas antes de empezar).

## Desvíos del spec (leer antes de arrancar)

Dos cosas que el spec pide no tienen datos que las sostengan. Se implementan
recortadas y **a propósito**; si el usuario prefiere otra cosa, se decide antes
de la Tarea 8, no durante.

1. **El rival `fish` / `reg` no está declarado en ningún JSON.** El spec dice
   "el rival etiquetado `fish` o `reg` según lo declara la tabla", pero eso hoy
   solo vive dentro de la clave (`HU_SB_OR_FISH`) y de la etiqueta
   (`"BB vs limp | fish"`). Deducirlo partiendo la clave viola la regla del
   proyecto —el formato se declara, no se deduce—. **Se muestra la
   `Etiqueta` de la situación tal cual**, que ya dice "| fish". El arreglo
   propio sería un campo `rival` en el JSON de cada tabla, y es su propia
   tarea futura.

2. **El bote y los stacks de cada jugador tampoco están declarados.** Una mesa
   simulada fiel los necesita y no existen: calcularlos por tipo de spot sería
   deducir de la clave otra vez. **La mesa muestra lo que los datos sostienen**:
   las dos cartas grandes con su palo, la banda de stack, la etiqueta del spot
   y la de la situación. Sin bote ni fichas del rival.

Un tercer punto, que es una simplificación y no un recorte:

3. **No hay "modo" en el intérprete.** El spec decía que el copiloto tiene que
   saber si un dictado es respuesta o consulta. No hace falta estado: la
   pantalla de entrenamiento manda su texto a `/api/entrenador/respuesta`, que
   usa un `InterpretadorDeRespuesta` propio. Quién sabe el modo es la pantalla,
   que ya lo sabe, y no hay una variable global que pueda quedar mal.

## Estructura de archivos

| archivo | responsabilidad |
|---|---|
| `src/PokerProOS.Domain/Entrenador/ProgresoDeCasilla.cs` | la fila: qué casilla, de quién, y su calendario |
| `src/PokerProOS.Domain/Manos/MatrizDeManos.cs` *(modificar)* | `Partir` — la etiqueta de mano a sus dos rangos y su palo |
| `src/PokerProOS.Application/Tablas/ICatalogoDeTablas.cs` *(modificar)* | `SpotDeTabla.EnElBorde` — sube desde `ResolverManoHandler` |
| `src/PokerProOS.Application/Entrenador/CalendarioDeRepeticion.cs` | puro: progreso + resultado + fecha → progreso nuevo |
| `src/PokerProOS.Application/Entrenador/ContratosDeEntrenador.cs` | `FiltroDeTanda`, `PreguntaDeTanda`, `RespuestaEnviada`, `VeredictoDeRespuesta` |
| `src/PokerProOS.Application/Entrenador/IProgresoDeEntrenamiento.cs` | puerto: leer vencidas, leer todas, buscar una, guardar |
| `src/PokerProOS.Application/Entrenador/PlanificadorDeTanda.cs` | puro: vencidas + catálogo + filtro + tamaño → preguntas |
| `src/PokerProOS.Application/Entrenador/ArmarTandaHandler.cs` | junta puerto y planificador |
| `src/PokerProOS.Application/Entrenador/ResponderRespuestaHandler.cs` | resuelve, compara, actualiza, arma la ficha al fallar |
| `src/PokerProOS.Application/Entrenador/InterpretadorDeRespuesta.cs` | texto → clave de acción, con los `dichos` de `acciones.json` |
| `src/PokerProOS.Infrastructure/Database/Configurations/ProgresoDeCasillaConfig.cs` | clave única con `UsuarioId` adelante |
| `src/PokerProOS.Infrastructure/Entrenador/ProgresoDeEntrenamientoSql.cs` | el puerto contra EF |
| `src/PokerProOS.Api/Controllers/EntrenadorController.cs` | los tres endpoints y el `UsuarioActual` |
| `frontend/src/features/entrenador/PaginaDeEntrenador.tsx` | el bucle: filtro → tanda → pregunta → veredicto |
| `frontend/src/features/entrenador/FiltroDeTanda.tsx` | sobre qué entrenar, armado del catálogo |
| `frontend/src/features/entrenador/MesaSimulada.tsx` | las dos cartas, el stack, el spot |
| `frontend/src/features/entrenador/BotonesDeAccion.tsx` | los botones del spot, con color y atajo del registro |
| `frontend/src/features/entrenador/Veredicto.tsx` | acierto o fallo; al fallar reusa `FichaDeMemoria.tsx` |

---

### Task 1: La fila del progreso

**Files:**
- Create: `src/PokerProOS.Domain/Entrenador/ProgresoDeCasilla.cs`
- Create: `src/PokerProOS.Infrastructure/Database/Configurations/ProgresoDeCasillaConfig.cs`
- Modify: `src/PokerProOS.Infrastructure/Database/PokerProOSDbContext.cs`
- Test: `tests/PokerProOS.Tests/Datos/ProgresoDeCasillaTests.cs`

**Interfaces:**
- Consumes: nada.
- Produces: `ProgresoDeCasilla` con propiedades `Id, UsuarioId, Situacion, ClaveDeStack, Spot, Mano, AciertosSeguidos, IntervaloEnDias, Vence, ActualizadaEn` y el estático `ProgresoDeCasilla.Clave(situacion, claveDeStack, spot, mano)`. `PokerProOSDbContext.ProgresosDeCasilla`.

- [ ] **Step 1: Escribir la prueba que falla**

Crear `tests/PokerProOS.Tests/Datos/ProgresoDeCasillaTests.cs`:

```csharp
using Microsoft.EntityFrameworkCore;
using PokerProOS.Domain.Entrenador;
using PokerProOS.Infrastructure.Database;

namespace PokerProOS.Tests.Datos;

public class ProgresoDeCasillaTests
{
    private static PokerProOSDbContext ContextoEnMemoria() =>
        new(new DbContextOptionsBuilder<PokerProOSDbContext>()
            .UseInMemoryDatabase($"prueba-{Guid.NewGuid():N}").Options);

    private static ProgresoDeCasilla Fila(int usuario = 1, string mano = "AKo") => new()
    {
        UsuarioId = usuario,
        Situacion = "HU_SB_OR_FISH",
        ClaveDeStack = "9-11bb",
        Spot = "SB_OR",
        Mano = mano,
        AciertosSeguidos = 1,
        IntervaloEnDias = 1,
        Vence = new DateOnly(2026, 8, 29),
    };

    [Fact]
    public async Task Guarda_y_relee_una_casilla()
    {
        using var contexto = ContextoEnMemoria();
        contexto.ProgresosDeCasilla.Add(Fila());
        await contexto.SaveChangesAsync();

        var fila = await contexto.ProgresosDeCasilla.SingleAsync();
        Assert.Equal("AKo", fila.Mano);
        Assert.Equal(new DateOnly(2026, 8, 29), fila.Vence);
    }

    /// <summary>
    /// La misma casilla de dos usuarios son dos filas. Es la razón por la que
    /// UsuarioId va en la clave desde el día uno: el día que haya login, el
    /// progreso ya está separado y no hay que migrar nada.
    /// </summary>
    [Fact]
    public async Task La_misma_casilla_de_dos_usuarios_son_dos_filas()
    {
        using var contexto = ContextoEnMemoria();
        contexto.ProgresosDeCasilla.AddRange(Fila(usuario: 1), Fila(usuario: 2));
        await contexto.SaveChangesAsync();

        Assert.Equal(2, await contexto.ProgresosDeCasilla.CountAsync());
    }

    /// <summary>
    /// La clave compuesta se arma en un solo lugar: el planificador la usa
    /// para saber qué casillas ya conoce, y dos formas distintas de armarla
    /// harían que material ya visto reapareciera como nuevo.
    /// </summary>
    [Fact]
    public void La_clave_compuesta_junta_los_cuatro_campos()
        => Assert.Equal(
            "HU_SB_OR_FISH|9-11bb|SB_OR|AKo",
            ProgresoDeCasilla.Clave("HU_SB_OR_FISH", "9-11bb", "SB_OR", "AKo"));
}
```

- [ ] **Step 2: Correr y verificar que falla**

Run: `dotnet test PokerProOS.slnx -p:SaltearFrontend=true --filter "FullyQualifiedName~ProgresoDeCasillaTests"`
Expected: FAIL — `error CS0246: no se encontró el tipo 'ProgresoDeCasilla'`.

- [ ] **Step 3: La entidad**

Crear `src/PokerProOS.Domain/Entrenador/ProgresoDeCasilla.cs`:

```csharp
namespace PokerProOS.Domain.Entrenador;

/// <summary>
/// Lo que se sabe de UNA casilla: usuario + situación + stack + spot + mano.
///
/// La unidad no es la mano sino la casilla, porque K2s en BB vs open shove a
/// 11bb es una cosa distinta de K2s en el mismo spot a 20bb: la respuesta
/// correcta cambia entre las dos, y aprender la tabla ES aprender dónde está
/// ese corte.
/// </summary>
public class ProgresoDeCasilla
{
    public int Id { get; set; }

    /// <summary>
    /// Va en la clave desde el día uno aunque todavía no haya login. El día
    /// que lo haya, el progreso ya está separado por persona y no hay datos
    /// que migrar.
    /// </summary>
    public int UsuarioId { get; set; }

    public string Situacion { get; set; } = "";
    public string ClaveDeStack { get; set; } = "";
    public string Spot { get; set; } = "";
    public string Mano { get; set; } = "";

    /// <summary>Cuántas veces seguidas se acertó. Un fallo la vuelve a cero.</summary>
    public int AciertosSeguidos { get; set; }

    /// <summary>Cuántos días duró el descanso que se acaba de conceder.</summary>
    public int IntervaloEnDias { get; set; }

    /// <summary>El día a partir del cual la casilla vuelve a entrar en una tanda.</summary>
    public DateOnly Vence { get; set; }

    public DateTime ActualizadaEn { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// La identidad de la casilla como texto, para juntarlas en un conjunto.
    /// Vive acá y no en quien la use para que el planificador y el repositorio
    /// no puedan armarla distinto: si lo hicieran, material ya estudiado
    /// reaparecería como nuevo.
    /// </summary>
    public static string Clave(string situacion, string claveDeStack, string spot, string mano)
        => $"{situacion}|{claveDeStack}|{spot}|{mano}";

    public string ClaveDeCasilla() => Clave(Situacion, ClaveDeStack, Spot, Mano);
}
```

- [ ] **Step 4: La configuración de EF**

Crear `src/PokerProOS.Infrastructure/Database/Configurations/ProgresoDeCasillaConfig.cs`:

```csharp
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using PokerProOS.Domain.Entrenador;

namespace PokerProOS.Infrastructure.Database.Configurations;

public class ProgresoDeCasillaConfig : IEntityTypeConfiguration<ProgresoDeCasilla>
{
    public void Configure(EntityTypeBuilder<ProgresoDeCasilla> constructor)
    {
        constructor.HasKey(e => e.Id);
        constructor.Property(e => e.Situacion).HasMaxLength(50).IsRequired();
        constructor.Property(e => e.ClaveDeStack).HasMaxLength(20).IsRequired();
        constructor.Property(e => e.Spot).HasMaxLength(50).IsRequired();
        constructor.Property(e => e.Mano).HasMaxLength(10).IsRequired();

        // Unico: una casilla tiene un solo calendario por persona. Sin esto,
        // dos respuestas concurrentes dejarian dos filas y el progreso se
        // partiria en dos calendarios que se pisan.
        constructor
            .HasIndex(e => new { e.UsuarioId, e.Situacion, e.ClaveDeStack, e.Spot, e.Mano })
            .IsUnique();

        // La pregunta que arma cada tanda: que le vencio hoy a esta persona.
        constructor.HasIndex(e => new { e.UsuarioId, e.Vence });
    }
}
```

- [ ] **Step 5: El DbSet**

En `src/PokerProOS.Infrastructure/Database/PokerProOSDbContext.cs`, agregar el `using` y la propiedad junto a las demás (después de `MarcasDeHabito`):

```csharp
using PokerProOS.Domain.Entrenador;
```

```csharp
    public DbSet<ProgresoDeCasilla> ProgresosDeCasilla => Set<ProgresoDeCasilla>();
```

- [ ] **Step 6: Correr y verificar que pasa**

Run: `dotnet test PokerProOS.slnx -p:SaltearFrontend=true --filter "FullyQualifiedName~ProgresoDeCasillaTests"`
Expected: PASS, 3 pruebas.

- [ ] **Step 7: La migración**

Run: `dotnet ef migrations add AgregaProgresoDeEntrenamiento --project src/PokerProOS.Infrastructure --startup-project src/PokerProOS.Api`

Verificar que el archivo nuevo en `src/PokerProOS.Infrastructure/Database/Migraciones/` crea la tabla `ProgresosDeCasilla` con el índice único. Si `dotnet ef` no está instalado: `dotnet tool install --global dotnet-ef`.

- [ ] **Step 8: La suite entera**

Run: `dotnet test PokerProOS.slnx -p:SaltearFrontend=true`
Expected: PASS, 246 pruebas.

- [ ] **Step 9: Commit**

```bash
git add src/PokerProOS.Domain/Entrenador src/PokerProOS.Infrastructure/Database tests/PokerProOS.Tests/Datos/ProgresoDeCasillaTests.cs
git commit -m "feat: la fila de progreso del entrenador, con el usuario en la clave"
```

---

### Task 2: El calendario de repetición

**Files:**
- Create: `src/PokerProOS.Application/Entrenador/CalendarioDeRepeticion.cs`
- Test: `tests/PokerProOS.Tests/Entrenador/CalendarioDeRepeticionTests.cs`

**Interfaces:**
- Consumes: nada. Es puro: no toca base, ni catálogo, ni reloj.
- Produces: `record ProgresoCalculado(int AciertosSeguidos, int IntervaloEnDias, DateOnly Vence)` y
  `CalendarioDeRepeticion.Siguiente(int aciertosSeguidos, bool acerto, DateOnly hoy) → ProgresoCalculado`,
  más `CalendarioDeRepeticion.Escalera` (`IReadOnlyList<int>`).

- [ ] **Step 1: Escribir las pruebas que fallan**

Crear `tests/PokerProOS.Tests/Entrenador/CalendarioDeRepeticionTests.cs`:

```csharp
using PokerProOS.Application.Entrenador;

namespace PokerProOS.Tests.Entrenador;

/// <summary>
/// La escalera de intervalos, sin base ni reloj: la fecha entra como
/// parámetro para que las pruebas no dependan del día en que se corren.
/// </summary>
public class CalendarioDeRepeticionTests
{
    private static readonly DateOnly Hoy = new(2026, 8, 28);

    [Theory]
    [InlineData(0, 1, 1)]   // primera vez que se acierta: descansa 1 día
    [InlineData(1, 2, 3)]
    [InlineData(2, 3, 7)]
    [InlineData(3, 4, 16)]
    [InlineData(4, 5, 35)]
    [InlineData(5, 6, 90)]
    public void Acertar_sube_un_escalon(int previos, int esperadosAciertos, int esperadoIntervalo)
    {
        var p = CalendarioDeRepeticion.Siguiente(previos, acerto: true, Hoy);

        Assert.Equal(esperadosAciertos, p.AciertosSeguidos);
        Assert.Equal(esperadoIntervalo, p.IntervaloEnDias);
        Assert.Equal(Hoy.AddDays(esperadoIntervalo), p.Vence);
    }

    /// <summary>
    /// Arriba de la escalera se queda en el último escalón. Sin este tope,
    /// el índice se saldría del arreglo en el séptimo acierto.
    /// </summary>
    [Fact]
    public void Sobre_el_ultimo_escalon_el_intervalo_no_crece_mas()
    {
        var p = CalendarioDeRepeticion.Siguiente(12, acerto: true, Hoy);

        Assert.Equal(13, p.AciertosSeguidos);
        Assert.Equal(90, p.IntervaloEnDias);
    }

    /// <summary>
    /// Fallar no baja un escalón: vuelve a cero. Media memoria de una casilla
    /// no es memoria, y el spec pide además que reentre en la tanda actual,
    /// por eso vence HOY y no mañana.
    /// </summary>
    [Fact]
    public void Fallar_resetea_y_vence_hoy()
    {
        var p = CalendarioDeRepeticion.Siguiente(5, acerto: false, Hoy);

        Assert.Equal(0, p.AciertosSeguidos);
        Assert.Equal(1, p.IntervaloEnDias);
        Assert.Equal(Hoy, p.Vence);
    }

    [Fact]
    public void La_escalera_es_la_del_spec()
        => Assert.Equal(new[] { 1, 3, 7, 16, 35, 90 }, CalendarioDeRepeticion.Escalera);
}
```

- [ ] **Step 2: Correr y verificar que falla**

Run: `dotnet test PokerProOS.slnx -p:SaltearFrontend=true --filter "FullyQualifiedName~CalendarioDeRepeticionTests"`
Expected: FAIL — `error CS0246: no se encontró el tipo 'CalendarioDeRepeticion'`.

- [ ] **Step 3: Implementar**

Crear `src/PokerProOS.Application/Entrenador/CalendarioDeRepeticion.cs`:

```csharp
namespace PokerProOS.Application.Entrenador;

/// <summary>Cómo queda una casilla después de contestarla.</summary>
public record ProgresoCalculado(int AciertosSeguidos, int IntervaloEnDias, DateOnly Vence);

/// <summary>
/// Cuándo vuelve a preguntarse una casilla.
///
/// Puro a propósito: sin base, sin catálogo y sin reloj propio —la fecha entra
/// como parámetro—. Así la regla se prueba entera con cuatro tests y no hay
/// forma de que el resultado dependa del día en que se corren.
/// </summary>
public static class CalendarioDeRepeticion
{
    /// <summary>
    /// Los descansos, en días. Cada acierto sube un escalón y el último se
    /// repite para siempre: una casilla que se sabe hace tres meses no
    /// necesita desaparecer, solo aparecer poco.
    /// </summary>
    public static IReadOnlyList<int> Escalera { get; } = [1, 3, 7, 16, 35, 90];

    public static ProgresoCalculado Siguiente(int aciertosSeguidos, bool acerto, DateOnly hoy)
    {
        // Fallar no baja un escalón: vuelve a cero. Y vence HOY, no mañana,
        // porque el spec pide que la casilla reentre en la tanda actual — es
        // el momento en que más sirve volver a verla.
        if (!acerto) return new ProgresoCalculado(0, Escalera[0], hoy);

        var nuevos = aciertosSeguidos + 1;
        var intervalo = Escalera[Math.Min(nuevos - 1, Escalera.Count - 1)];
        return new ProgresoCalculado(nuevos, intervalo, hoy.AddDays(intervalo));
    }
}
```

- [ ] **Step 4: Correr y verificar que pasa**

Run: `dotnet test PokerProOS.slnx -p:SaltearFrontend=true --filter "FullyQualifiedName~CalendarioDeRepeticionTests"`
Expected: PASS, 9 pruebas (6 del `Theory` + 3).

- [ ] **Step 5: Commit**

```bash
git add src/PokerProOS.Application/Entrenador tests/PokerProOS.Tests/Entrenador
git commit -m "feat: la escalera de intervalos del entrenador"
```

---

### Task 3: El planificador de la tanda

**Files:**
- Modify: `src/PokerProOS.Application/Tablas/ICatalogoDeTablas.cs` (agregar `SpotDeTabla.EnElBorde`)
- Modify: `src/PokerProOS.Application/Tablas/ResolverManoHandler.cs` (usarlo en vez de repetir el cálculo)
- Create: `src/PokerProOS.Application/Entrenador/ContratosDeEntrenador.cs`
- Create: `src/PokerProOS.Application/Entrenador/IProgresoDeEntrenamiento.cs`
- Create: `src/PokerProOS.Application/Entrenador/PlanificadorDeTanda.cs`
- Test: `tests/PokerProOS.Tests/Entrenador/PlanificadorDeTandaTests.cs`

**Interfaces:**
- Consumes: `ICatalogoDeTablas`, `SituacionDeTabla`, `TablaDeStack`, `SpotDeTabla`, `CeldaDeTabla`, `ProgresoDeCasilla` (Task 1).
- Produces:
  - `SpotDeTabla.EnElBorde(string mano) → bool`
  - `record FiltroDeTanda(string? Formato, string? Situacion, decimal? MinBB, decimal? MaxBB, string? Spot)`
  - `record PreguntaDeTanda(string Situacion, string EtiquetaDeSituacion, string ClaveDeStack, string Spot, string EtiquetaDeSpot, string Mano, bool EsNueva)`
  - `IProgresoDeEntrenamiento` con `VencidasAsync`, `TodasAsync`, `BuscarAsync`, `GuardarAsync`
  - `PlanificadorDeTanda.Planificar(IReadOnlyList<ProgresoDeCasilla> vencidas, IReadOnlyCollection<string> yaConocidas, FiltroDeTanda filtro, int tamano) → IReadOnlyList<PreguntaDeTanda>`

- [ ] **Step 1: Escribir la prueba que falla**

Crear `tests/PokerProOS.Tests/Entrenador/PlanificadorDeTandaTests.cs`:

```csharp
using PokerProOS.Application.Entrenador;
using PokerProOS.Application.Tablas;
using PokerProOS.Domain.Entrenador;
using PokerProOS.Domain.Manos;
using PokerProOS.Domain.Tablas;
using PokerProOS.Infrastructure.Tablas;

namespace PokerProOS.Tests.Entrenador;

/// <summary>
/// Catálogo sintético, como ya hace AnalizadorDeMemoriaTests: la regla del
/// planificador se prueba contra una tabla inventada y chica, no contra las
/// del repo, que cambian.
/// </summary>
public class PlanificadorDeTandaTests
{
    /// <summary>
    /// Un spot donde TODO es FOLD salvo AA, que es ALL-IN. Así solo hay un
    /// borde y es fácil de nombrar en las aserciones.
    /// </summary>
    private static SpotDeTabla SpotConUnSoloBorde(string clave) => new(
        clave, $"etiqueta de {clave}",
        MatrizDeManos.Todas()
            .Select(m => new CeldaDeTabla(m, m == "AA" ? "ALL-IN" : "FOLD"))
            .ToList());

    private static ICatalogoDeTablas Catalogo() => new CatalogoEnMemoria(
        [
            new SituacionDeTabla("HU_X", "HU equis | fish", "HU",
            [
                new TablaDeStack(new RangoDeStack("1-5bb", 1, 5), [SpotConUnSoloBorde("SB_OR")]),
                new TablaDeStack(new RangoDeStack("6-9bb", 6, 9), [SpotConUnSoloBorde("SB_OR")]),
            ]),
            new SituacionDeTabla("MAX3_X", "3max equis | fish fish", "3-max",
            [
                new TablaDeStack(new RangoDeStack("1-5bb", 1, 5), [SpotConUnSoloBorde("BTN_OR")]),
            ]),
        ], []);

    private static ProgresoDeCasilla Vencida(string mano, DateOnly vence, string stack = "1-5bb") => new()
    {
        UsuarioId = 1, Situacion = "HU_X", ClaveDeStack = stack, Spot = "SB_OR",
        Mano = mano, AciertosSeguidos = 0, IntervaloEnDias = 1, Vence = vence,
    };

    private static readonly FiltroDeTanda SinFiltro = new(null, null, null, null, null);

    /// <summary>
    /// Lo vencido va primero y lo más vencido antes: si la tanda no alcanza
    /// para todo, lo que más tiempo lleva sin verse es lo que más urge.
    /// </summary>
    [Fact]
    public void Lo_mas_vencido_va_primero()
    {
        var preguntas = new PlanificadorDeTanda(Catalogo()).Planificar(
            [
                Vencida("KK", new DateOnly(2026, 8, 27)),
                Vencida("QQ", new DateOnly(2026, 8, 20)),
                Vencida("JJ", new DateOnly(2026, 8, 25)),
            ],
            yaConocidas: [],
            SinFiltro,
            tamano: 3);

        Assert.Equal(["QQ", "JJ", "KK"], preguntas.Select(p => p.Mano));
        Assert.All(preguntas, p => Assert.False(p.EsNueva));
    }

    /// <summary>
    /// Si lo vencido no llena la tanda, se completa con material nuevo, y ese
    /// material prioriza los bordes: son las casillas que separan saber la
    /// tabla de adivinarla. Acá el único borde del spot es AA.
    /// </summary>
    [Fact]
    public void El_relleno_empieza_por_los_bordes()
    {
        var preguntas = new PlanificadorDeTanda(Catalogo()).Planificar(
            [Vencida("KK", new DateOnly(2026, 8, 27))],
            yaConocidas: [],
            SinFiltro,
            tamano: 2);

        Assert.Equal("KK", preguntas[0].Mano);
        Assert.True(preguntas[1].EsNueva);
        Assert.Equal("AA", preguntas[1].Mano);
    }

    /// <summary>
    /// El relleno no repite lo que ya se estudió: una casilla con progreso que
    /// todavía no vence no es material nuevo.
    /// </summary>
    [Fact]
    public void El_relleno_saltea_lo_que_ya_se_conoce()
    {
        var preguntas = new PlanificadorDeTanda(Catalogo()).Planificar(
            vencidas: [],
            yaConocidas: [ProgresoDeCasilla.Clave("HU_X", "1-5bb", "SB_OR", "AA")],
            SinFiltro,
            tamano: 1);

        Assert.DoesNotContain(preguntas,
            p => p is { Situacion: "HU_X", ClaveDeStack: "1-5bb", Mano: "AA" });
    }

    [Fact]
    public void El_filtro_de_formato_deja_afuera_las_otras_mesas()
    {
        var preguntas = new PlanificadorDeTanda(Catalogo()).Planificar(
            vencidas: [],
            yaConocidas: [],
            new FiltroDeTanda("3-max", null, null, null, null),
            tamano: 20);

        Assert.NotEmpty(preguntas);
        Assert.All(preguntas, p => Assert.Equal("MAX3_X", p.Situacion));
    }

    /// <summary>
    /// El rango de stack se compara contra la cobertura real de cada tabla,
    /// no contra su clave: "6-9bb" entra en un filtro de 7 a 12 porque las dos
    /// bandas se tocan.
    /// </summary>
    [Fact]
    public void El_filtro_de_stack_mira_la_cobertura_de_la_tabla()
    {
        var preguntas = new PlanificadorDeTanda(Catalogo()).Planificar(
            vencidas: [],
            yaConocidas: [],
            new FiltroDeTanda(null, null, 7m, 12m, null),
            tamano: 20);

        Assert.NotEmpty(preguntas);
        Assert.All(preguntas, p => Assert.Equal("6-9bb", p.ClaveDeStack));
    }

    /// <summary>
    /// Una casilla vencida de una tabla que ya no existe se ignora en vez de
    /// romper: las tablas se corrigen a mano y un spot puede desaparecer,
    /// dejando progreso huérfano que no hay que preguntar.
    /// </summary>
    [Fact]
    public void Una_vencida_que_ya_no_existe_en_el_catalogo_se_ignora()
    {
        var huerfana = Vencida("KK", new DateOnly(2026, 8, 1));
        huerfana.Spot = "SPOT_QUE_NO_EXISTE";

        var preguntas = new PlanificadorDeTanda(Catalogo()).Planificar(
            [huerfana], yaConocidas: [], SinFiltro, tamano: 5);

        Assert.DoesNotContain(preguntas, p => p.Spot == "SPOT_QUE_NO_EXISTE");
    }

    [Fact]
    public void La_tanda_no_pasa_del_tamano_pedido()
    {
        var preguntas = new PlanificadorDeTanda(Catalogo()).Planificar(
            vencidas: [], yaConocidas: [], SinFiltro, tamano: 4);

        Assert.Equal(4, preguntas.Count);
    }
}
```

- [ ] **Step 2: Correr y verificar que falla**

Run: `dotnet test PokerProOS.slnx -p:SaltearFrontend=true --filter "FullyQualifiedName~PlanificadorDeTandaTests"`
Expected: FAIL — `error CS0246: no se encontró el tipo 'PlanificadorDeTanda'`.

- [ ] **Step 3: Subir `EnElBorde` a `SpotDeTabla`**

En `src/PokerProOS.Application/Tablas/ICatalogoDeTablas.cs`, agregar el `using` arriba y el método dentro de `SpotDeTabla`, después de `CeldaDe`:

```csharp
using PokerProOS.Domain.Manos;
```

```csharp
    /// <summary>
    /// Si la mano está en el filo de su bloque: alguna vecina de la matriz
    /// tiene otra acción, o la propia celda es mixta —una mano mixta es un
    /// borde por definición, la tabla misma dice que ahí no hay respuesta
    /// única—.
    ///
    /// Vive acá y no en quien pregunta porque lo necesitan dos: el resolvedor,
    /// para avisar por voz, y el planificador, para elegir qué material nuevo
    /// enseña algo. Dos copias del cálculo se despegarían.
    /// </summary>
    public bool EnElBorde(string mano)
    {
        var accion = AccionDe(mano);
        if (accion is null) return false;
        if (CeldaDe(mano)?.EsMixta == true) return true;

        return MatrizDeManos.Vecinas(mano).Any(vecina =>
            !string.Equals(AccionDe(vecina), accion, StringComparison.OrdinalIgnoreCase));
    }
```

- [ ] **Step 4: Que el resolvedor lo use en vez de repetirlo**

En `src/PokerProOS.Application/Tablas/ResolverManoHandler.cs`, reemplazar:

```csharp
        // Una mano mixta es un borde por definicion: la tabla misma dice que
        // no hay una respuesta unica ahi.
        var enElBorde = celda?.EsMixta == true
            || MatrizDeManos.Vecinas(mano)
                .Any(vecina => !string.Equals(spot.AccionDe(vecina), accion, StringComparison.OrdinalIgnoreCase));
```

por:

```csharp
        var enElBorde = spot.EnElBorde(mano);
```

- [ ] **Step 5: Los contratos y el puerto**

Crear `src/PokerProOS.Application/Entrenador/ContratosDeEntrenador.cs`:

```csharp
using PokerProOS.Application.Tablas;
using PokerProOS.Domain.Tablas;

namespace PokerProOS.Application.Entrenador;

/// <summary>
/// Sobre qué entrenar. Todo opcional: sin nada elegido entra el catálogo
/// entero. El rango de stack va en BB y se compara contra la cobertura real de
/// cada tabla, no contra su clave.
/// </summary>
public record FiltroDeTanda(
    string? Formato, string? Situacion, decimal? MinBB, decimal? MaxBB, string? Spot);

/// <summary>
/// Una pregunta de la tanda. Trae las etiquetas ya resueltas porque la
/// pantalla las muestra y pedírselas al catálogo otra vez sería un segundo
/// viaje para un dato que acá está a mano.
/// </summary>
public record PreguntaDeTanda(
    string Situacion,
    string EtiquetaDeSituacion,
    string ClaveDeStack,
    string Spot,
    string EtiquetaDeSpot,
    string Mano,
    /// <summary>Material nuevo, sin progreso previo. La pantalla lo distingue.</summary>
    bool EsNueva);

/// <summary>Lo que la pantalla manda al contestar.</summary>
public record RespuestaEnviada(
    string Situacion, string ClaveDeStack, string Spot, string Mano, string Accion);

/// <summary>
/// Qué pasó con la respuesta. La ficha viene solo al fallar: acertar sigue de
/// largo, y es al errar cuando una explicación entra de verdad.
/// </summary>
public record VeredictoDeRespuesta(
    bool Acerto,
    string AccionCorrecta,
    IReadOnlyList<ParteDeMix>? Mix,
    FichaDeMemoria? Ficha,
    DateOnly Vence);
```

Crear `src/PokerProOS.Application/Entrenador/IProgresoDeEntrenamiento.cs`:

```csharp
using PokerProOS.Domain.Entrenador;

namespace PokerProOS.Application.Entrenador;

/// <summary>
/// El progreso, sin decir dónde vive. Es el único puerto del entrenador que
/// necesita base: todo lo demás sale del catálogo en memoria.
/// </summary>
public interface IProgresoDeEntrenamiento
{
    /// <summary>Las casillas cuyo día ya llegó, de más vencida a menos.</summary>
    Task<IReadOnlyList<ProgresoDeCasilla>> VencidasAsync(
        int usuarioId, DateOnly hoy, CancellationToken ct);

    /// <summary>
    /// Todo lo que esta persona alguna vez contestó. Sirve para una sola cosa:
    /// que el material nuevo no repita casillas ya estudiadas.
    /// </summary>
    Task<IReadOnlyList<ProgresoDeCasilla>> TodasAsync(int usuarioId, CancellationToken ct);

    Task<ProgresoDeCasilla?> BuscarAsync(
        int usuarioId, string situacion, string claveDeStack, string spot, string mano,
        CancellationToken ct);

    Task GuardarAsync(ProgresoDeCasilla progreso, CancellationToken ct);
}
```

- [ ] **Step 6: El planificador**

Crear `src/PokerProOS.Application/Entrenador/PlanificadorDeTanda.cs`:

```csharp
using PokerProOS.Application.Tablas;
using PokerProOS.Domain.Entrenador;

namespace PokerProOS.Application.Entrenador;

/// <summary>
/// Arma la tanda: lo vencido primero y, si sobra lugar, material nuevo.
///
/// Puro: recibe las vencidas ya leídas y no toca la base. Lo que sí necesita
/// es el catálogo, porque una casilla vencida puede haber dejado de existir
/// —las tablas se corrigen a mano— y porque el material nuevo sale de ahí.
///
/// El orden del relleno es determinista, no al azar. Hay más de 57.000
/// casillas y al azar no se cubren nunca; además, una vez contestada, una
/// casilla deja de ser nueva, así que el recorrido avanza solo.
/// </summary>
public sealed class PlanificadorDeTanda(ICatalogoDeTablas catalogo)
{
    public IReadOnlyList<PreguntaDeTanda> Planificar(
        IReadOnlyList<ProgresoDeCasilla> vencidas,
        IReadOnlyCollection<string> yaConocidas,
        FiltroDeTanda filtro,
        int tamano)
    {
        if (tamano <= 0) return [];

        var elegidas = new List<PreguntaDeTanda>();

        foreach (var vencida in vencidas.OrderBy(v => v.Vence).ThenBy(v => v.Mano))
        {
            if (elegidas.Count == tamano) break;
            if (Pregunta(vencida, filtro) is { } pregunta) elegidas.Add(pregunta);
        }

        if (elegidas.Count == tamano) return elegidas;

        // El relleno no puede repetir ni lo ya estudiado ni lo que acaba de
        // entrar por vencido.
        var vistas = new HashSet<string>(yaConocidas, StringComparer.OrdinalIgnoreCase);
        foreach (var p in elegidas)
            vistas.Add(ProgresoDeCasilla.Clave(p.Situacion, p.ClaveDeStack, p.Spot, p.Mano));

        foreach (var nueva in Nuevas(filtro, vistas))
        {
            if (elegidas.Count == tamano) break;
            elegidas.Add(nueva);
        }

        return elegidas;
    }

    /// <summary>
    /// La vencida convertida en pregunta, o null si el filtro la deja afuera o
    /// si su casilla ya no existe en el catálogo. Progreso huérfano no es un
    /// error: un spot puede desaparecer al corregir una tabla.
    /// </summary>
    private PreguntaDeTanda? Pregunta(ProgresoDeCasilla vencida, FiltroDeTanda filtro)
    {
        var situacion = catalogo.Situacion(vencida.Situacion);
        if (situacion is null || !PasaSituacion(situacion, filtro)) return null;

        var tabla = catalogo.StackPorClave(vencida.Situacion, vencida.ClaveDeStack);
        if (tabla is null || !PasaStack(tabla, filtro)) return null;

        var spot = tabla.Spot(vencida.Spot);
        if (spot is null || !PasaSpot(spot, filtro)) return null;
        if (spot.AccionDe(vencida.Mano) is null) return null;

        return new PreguntaDeTanda(
            situacion.Clave, situacion.Etiqueta,
            tabla.Stack.Clave,
            spot.Clave, spot.Etiqueta,
            vencida.Mano,
            EsNueva: false);
    }

    /// <summary>
    /// Material nuevo, con los bordes adelante. Un borde es donde se corta el
    /// bloque de una familia o cambia el umbral de stack: son las casillas que
    /// separan saber la tabla de adivinarla. El resto va después para que la
    /// tanda igual se llene cuando los bordes se agotan.
    /// </summary>
    private IEnumerable<PreguntaDeTanda> Nuevas(FiltroDeTanda filtro, HashSet<string> vistas)
    {
        var candidatas = new List<(bool Borde, PreguntaDeTanda Pregunta)>();

        foreach (var situacion in catalogo.Situaciones)
        {
            if (!PasaSituacion(situacion, filtro)) continue;

            foreach (var tabla in situacion.Stacks)
            {
                if (!PasaStack(tabla, filtro)) continue;

                foreach (var spot in tabla.Spots)
                {
                    if (!PasaSpot(spot, filtro)) continue;

                    foreach (var celda in spot.Celdas)
                    {
                        var clave = ProgresoDeCasilla.Clave(
                            situacion.Clave, tabla.Stack.Clave, spot.Clave, celda.Mano);
                        if (!vistas.Add(clave)) continue;

                        candidatas.Add((
                            spot.EnElBorde(celda.Mano),
                            new PreguntaDeTanda(
                                situacion.Clave, situacion.Etiqueta,
                                tabla.Stack.Clave,
                                spot.Clave, spot.Etiqueta,
                                celda.Mano,
                                EsNueva: true)));
                    }
                }
            }
        }

        return candidatas.OrderByDescending(c => c.Borde).Select(c => c.Pregunta);
    }

    private static bool PasaSituacion(SituacionDeTabla situacion, FiltroDeTanda filtro)
        => (filtro.Formato is not { Length: > 0 }
            || string.Equals(situacion.Formato, filtro.Formato, StringComparison.OrdinalIgnoreCase))
           && (filtro.Situacion is not { Length: > 0 }
            || string.Equals(situacion.Clave, filtro.Situacion, StringComparison.OrdinalIgnoreCase));

    /// <summary>
    /// El filtro de stack se compara contra la cobertura de la tabla, no
    /// contra su clave: entra toda tabla cuya banda se toque con la pedida.
    /// </summary>
    private static bool PasaStack(TablaDeStack tabla, FiltroDeTanda filtro)
        => (filtro.MinBB is not { } min || tabla.Stack.MaxBB >= min)
           && (filtro.MaxBB is not { } max || tabla.Stack.MinBB <= max);

    private static bool PasaSpot(SpotDeTabla spot, FiltroDeTanda filtro)
        => filtro.Spot is not { Length: > 0 }
           || string.Equals(spot.Clave, filtro.Spot, StringComparison.OrdinalIgnoreCase);
}
```

- [ ] **Step 7: Correr y verificar que pasa**

Run: `dotnet test PokerProOS.slnx -p:SaltearFrontend=true --filter "FullyQualifiedName~PlanificadorDeTandaTests"`
Expected: PASS, 7 pruebas.

- [ ] **Step 8: La suite entera, porque se tocó el resolvedor**

Run: `dotnet test PokerProOS.slnx -p:SaltearFrontend=true`
Expected: PASS, 262 pruebas. Si alguna de `ResolverManoHandler` falla, el
método `EnElBorde` no está devolviendo lo mismo que el cálculo que reemplazó:
revisar el orden de las condiciones antes de tocar las pruebas.

- [ ] **Step 9: Commit**

```bash
git add src/PokerProOS.Application tests/PokerProOS.Tests/Entrenador
git commit -m "feat: el planificador de la tanda, con los bordes adelante"
```

---

### Task 4: El progreso contra la base

**Files:**
- Create: `src/PokerProOS.Infrastructure/Entrenador/ProgresoDeEntrenamientoSql.cs`
- Test: `tests/PokerProOS.Tests/Datos/ProgresoDeEntrenamientoSqlTests.cs`

**Interfaces:**
- Consumes: `IProgresoDeEntrenamiento` y `ProgresoDeCasilla` (Tasks 1 y 3), `PokerProOSDbContext`.
- Produces: `ProgresoDeEntrenamientoSql(PokerProOSDbContext contexto)`.

- [ ] **Step 1: Escribir la prueba que falla**

Crear `tests/PokerProOS.Tests/Datos/ProgresoDeEntrenamientoSqlTests.cs`:

```csharp
using Microsoft.EntityFrameworkCore;
using PokerProOS.Domain.Entrenador;
using PokerProOS.Infrastructure.Database;
using PokerProOS.Infrastructure.Entrenador;

namespace PokerProOS.Tests.Datos;

public class ProgresoDeEntrenamientoSqlTests
{
    private static PokerProOSDbContext ContextoEnMemoria() =>
        new(new DbContextOptionsBuilder<PokerProOSDbContext>()
            .UseInMemoryDatabase($"prueba-{Guid.NewGuid():N}").Options);

    private static readonly DateOnly Hoy = new(2026, 8, 28);

    private static ProgresoDeCasilla Fila(string mano, DateOnly vence, int usuario = 1) => new()
    {
        UsuarioId = usuario, Situacion = "HU_SB_OR_FISH", ClaveDeStack = "9-11bb",
        Spot = "SB_OR", Mano = mano, AciertosSeguidos = 0, IntervaloEnDias = 1, Vence = vence,
    };

    [Fact]
    public async Task Vencidas_trae_lo_de_hoy_y_lo_de_antes_pero_no_lo_de_manana()
    {
        using var contexto = ContextoEnMemoria();
        contexto.ProgresosDeCasilla.AddRange(
            Fila("AA", Hoy.AddDays(-3)),
            Fila("KK", Hoy),
            Fila("QQ", Hoy.AddDays(1)));
        await contexto.SaveChangesAsync();

        var vencidas = await new ProgresoDeEntrenamientoSql(contexto)
            .VencidasAsync(1, Hoy, CancellationToken.None);

        Assert.Equal(["AA", "KK"], vencidas.Select(v => v.Mano));
    }

    /// <summary>
    /// El progreso es de cada persona: sin filtrar por usuario, la primera que
    /// entrene le arruina la tanda a la siguiente.
    /// </summary>
    [Fact]
    public async Task Vencidas_no_mezcla_usuarios()
    {
        using var contexto = ContextoEnMemoria();
        contexto.ProgresosDeCasilla.AddRange(
            Fila("AA", Hoy, usuario: 1),
            Fila("KK", Hoy, usuario: 2));
        await contexto.SaveChangesAsync();

        var vencidas = await new ProgresoDeEntrenamientoSql(contexto)
            .VencidasAsync(1, Hoy, CancellationToken.None);

        Assert.Equal("AA", Assert.Single(vencidas).Mano);
    }

    [Fact]
    public async Task Guardar_una_fila_nueva_la_inserta()
    {
        using var contexto = ContextoEnMemoria();
        var repositorio = new ProgresoDeEntrenamientoSql(contexto);

        await repositorio.GuardarAsync(Fila("AA", Hoy), CancellationToken.None);

        Assert.Equal(1, await contexto.ProgresosDeCasilla.CountAsync());
    }

    /// <summary>
    /// Volver a contestar la misma casilla actualiza su fila, no agrega otra:
    /// dos filas para una casilla serían dos calendarios que se pisan.
    /// </summary>
    [Fact]
    public async Task Guardar_una_fila_existente_la_actualiza()
    {
        using var contexto = ContextoEnMemoria();
        var repositorio = new ProgresoDeEntrenamientoSql(contexto);
        await repositorio.GuardarAsync(Fila("AA", Hoy), CancellationToken.None);

        var traida = await repositorio.BuscarAsync(
            1, "HU_SB_OR_FISH", "9-11bb", "SB_OR", "AA", CancellationToken.None);
        traida!.AciertosSeguidos = 3;
        traida.Vence = Hoy.AddDays(7);
        await repositorio.GuardarAsync(traida, CancellationToken.None);

        var fila = await contexto.ProgresosDeCasilla.SingleAsync();
        Assert.Equal(3, fila.AciertosSeguidos);
        Assert.Equal(Hoy.AddDays(7), fila.Vence);
    }

    [Fact]
    public async Task Buscar_una_casilla_que_no_existe_devuelve_null()
    {
        using var contexto = ContextoEnMemoria();

        var nada = await new ProgresoDeEntrenamientoSql(contexto).BuscarAsync(
            1, "HU_SB_OR_FISH", "9-11bb", "SB_OR", "AA", CancellationToken.None);

        Assert.Null(nada);
    }
}
```

- [ ] **Step 2: Correr y verificar que falla**

Run: `dotnet test PokerProOS.slnx -p:SaltearFrontend=true --filter "FullyQualifiedName~ProgresoDeEntrenamientoSqlTests"`
Expected: FAIL — `error CS0246: no se encontró el tipo 'ProgresoDeEntrenamientoSql'`.

- [ ] **Step 3: Implementar**

Crear `src/PokerProOS.Infrastructure/Entrenador/ProgresoDeEntrenamientoSql.cs`:

```csharp
using Microsoft.EntityFrameworkCore;
using PokerProOS.Application.Entrenador;
using PokerProOS.Domain.Entrenador;
using PokerProOS.Infrastructure.Database;

namespace PokerProOS.Infrastructure.Entrenador;

/// <summary>
/// El progreso contra EF. Sin try/catch: a diferencia de la bitácora, acá una
/// base caída NO se traga en silencio — un calendario de repetición que pierde
/// respuestas no es un calendario, y el spec pide que el entrenador lo diga en
/// pantalla en vez de fallar callado. Quien traduce la excepción a un mensaje
/// es el controlador.
/// </summary>
public sealed class ProgresoDeEntrenamientoSql(PokerProOSDbContext contexto)
    : IProgresoDeEntrenamiento
{
    public async Task<IReadOnlyList<ProgresoDeCasilla>> VencidasAsync(
        int usuarioId, DateOnly hoy, CancellationToken ct)
        => await contexto.ProgresosDeCasilla
            .Where(p => p.UsuarioId == usuarioId && p.Vence <= hoy)
            .OrderBy(p => p.Vence).ThenBy(p => p.Mano)
            .ToListAsync(ct);

    public async Task<IReadOnlyList<ProgresoDeCasilla>> TodasAsync(
        int usuarioId, CancellationToken ct)
        => await contexto.ProgresosDeCasilla
            .Where(p => p.UsuarioId == usuarioId)
            .ToListAsync(ct);

    public Task<ProgresoDeCasilla?> BuscarAsync(
        int usuarioId, string situacion, string claveDeStack, string spot, string mano,
        CancellationToken ct)
        => contexto.ProgresosDeCasilla.FirstOrDefaultAsync(
            p => p.UsuarioId == usuarioId
                 && p.Situacion == situacion
                 && p.ClaveDeStack == claveDeStack
                 && p.Spot == spot
                 && p.Mano == mano,
            ct);

    public async Task GuardarAsync(ProgresoDeCasilla progreso, CancellationToken ct)
    {
        progreso.ActualizadaEn = DateTime.UtcNow;
        // Id 0 es una fila que nunca se guardó. Una que vino de BuscarAsync ya
        // la está siguiendo el contexto, así que alcanza con SaveChanges.
        if (progreso.Id == 0) contexto.ProgresosDeCasilla.Add(progreso);
        await contexto.SaveChangesAsync(ct);
    }
}
```

- [ ] **Step 4: Correr y verificar que pasa**

Run: `dotnet test PokerProOS.slnx -p:SaltearFrontend=true --filter "FullyQualifiedName~ProgresoDeEntrenamientoSqlTests"`
Expected: PASS, 5 pruebas.

- [ ] **Step 5: Commit**

```bash
git add src/PokerProOS.Infrastructure/Entrenador tests/PokerProOS.Tests/Datos/ProgresoDeEntrenamientoSqlTests.cs
git commit -m "feat: el progreso del entrenador contra la base"
```

---

### Task 5: Los dos handlers

**Files:**
- Modify: `src/PokerProOS.Domain/Manos/MatrizDeManos.cs` (agregar `Partir`)
- Modify: `src/PokerProOS.Application/Voz/InterpretadorDeTexto.cs` (usar `MatrizDeManos.Partir` en vez de su copia privada)
- Create: `src/PokerProOS.Application/Entrenador/ArmarTandaHandler.cs`
- Create: `src/PokerProOS.Application/Entrenador/ResponderRespuestaHandler.cs`
- Test: `tests/PokerProOS.Tests/Entrenador/ResponderRespuestaHandlerTests.cs`

**Interfaces:**
- Consumes: `IProgresoDeEntrenamiento`, `PlanificadorDeTanda`, `CalendarioDeRepeticion`, `ResolverManoHandler`, `AnalizadorDeMemoria`, `ICatalogoDeTablas`, `RespuestaEnviada`, `VeredictoDeRespuesta`.
- Produces:
  - `MatrizDeManos.Partir(string etiqueta) → (string Alto, string Bajo, string? Palo)`
  - `ArmarTandaHandler.ArmarAsync(int usuarioId, FiltroDeTanda filtro, int tamano, DateOnly hoy, CancellationToken ct) → Task<IReadOnlyList<PreguntaDeTanda>>`
  - `ResponderRespuestaHandler.ResponderAsync(int usuarioId, RespuestaEnviada respuesta, DateOnly hoy, CancellationToken ct) → Task<VeredictoDeRespuesta?>` (null si la casilla no existe en el catálogo)

- [ ] **Step 1: Escribir la prueba que falla**

Crear `tests/PokerProOS.Tests/Entrenador/ResponderRespuestaHandlerTests.cs`:

```csharp
using PokerProOS.Application.Entrenador;
using PokerProOS.Application.Tablas;
using PokerProOS.Domain.Entrenador;
using PokerProOS.Domain.Manos;
using PokerProOS.Domain.Tablas;
using PokerProOS.Infrastructure.Tablas;

namespace PokerProOS.Tests.Entrenador;

public class ResponderRespuestaHandlerTests
{
    private static readonly DateOnly Hoy = new(2026, 8, 28);

    /// <summary>
    /// AA es un mix mitad y mitad; KK es ALL-IN puro; el resto FOLD. Con eso
    /// alcanza para acierto, fallo y mano mixta.
    /// </summary>
    private static ICatalogoDeTablas Catalogo()
    {
        var celdas = MatrizDeManos.Todas().Select(m => m switch
        {
            "AA" => new CeldaDeTabla(m, "ALL-IN",
                [new ParteDeMix("ALL-IN", 50), new ParteDeMix("CALL", 50)]),
            "KK" => new CeldaDeTabla(m, "ALL-IN"),
            _ => new CeldaDeTabla(m, "FOLD"),
        }).ToList();

        return new CatalogoEnMemoria(
            [
                new SituacionDeTabla("HU_X", "HU equis | fish", "HU",
                [
                    new TablaDeStack(new RangoDeStack("9-11bb", 9, 11),
                    [
                        new SpotDeTabla("SB_OR", "SB abre", celdas),
                    ]),
                ]),
            ], []);
    }

    private sealed class ProgresoEnMemoria : IProgresoDeEntrenamiento
    {
        public List<ProgresoDeCasilla> Filas { get; } = [];

        public Task<IReadOnlyList<ProgresoDeCasilla>> VencidasAsync(
            int usuarioId, DateOnly hoy, CancellationToken ct)
            => Task.FromResult<IReadOnlyList<ProgresoDeCasilla>>(
                Filas.Where(f => f.UsuarioId == usuarioId && f.Vence <= hoy).ToList());

        public Task<IReadOnlyList<ProgresoDeCasilla>> TodasAsync(int usuarioId, CancellationToken ct)
            => Task.FromResult<IReadOnlyList<ProgresoDeCasilla>>(
                Filas.Where(f => f.UsuarioId == usuarioId).ToList());

        public Task<ProgresoDeCasilla?> BuscarAsync(
            int usuarioId, string situacion, string claveDeStack, string spot, string mano,
            CancellationToken ct)
            => Task.FromResult(Filas.FirstOrDefault(f =>
                f.UsuarioId == usuarioId && f.Situacion == situacion
                && f.ClaveDeStack == claveDeStack && f.Spot == spot && f.Mano == mano));

        public Task GuardarAsync(ProgresoDeCasilla progreso, CancellationToken ct)
        {
            if (!Filas.Contains(progreso)) Filas.Add(progreso);
            return Task.CompletedTask;
        }
    }

    private static (ResponderRespuestaHandler Handler, ProgresoEnMemoria Progreso) Armar()
    {
        var catalogo = Catalogo();
        var progreso = new ProgresoEnMemoria();
        return (new ResponderRespuestaHandler(
            new ResolverManoHandler(catalogo),
            new AnalizadorDeMemoria(catalogo),
            catalogo,
            progreso), progreso);
    }

    private static RespuestaEnviada Enviada(string mano, string accion)
        => new("HU_X", "9-11bb", "SB_OR", mano, accion);

    [Fact]
    public async Task Acertar_avanza_el_calendario_y_no_trae_ficha()
    {
        var (handler, progreso) = Armar();

        var v = await handler.ResponderAsync(1, Enviada("KK", "ALL-IN"), Hoy, default);

        Assert.True(v!.Acerto);
        Assert.Null(v.Ficha);
        Assert.Equal(Hoy.AddDays(1), v.Vence);
        Assert.Equal(1, progreso.Filas.Single().AciertosSeguidos);
    }

    /// <summary>
    /// Al fallar viene la ficha entera: es el momento en que más sirve, y es
    /// justo el que el entrenador de PokerHero desaprovecha.
    /// </summary>
    [Fact]
    public async Task Fallar_trae_la_ficha_y_vuelve_a_vencer_hoy()
    {
        var (handler, progreso) = Armar();

        var v = await handler.ResponderAsync(1, Enviada("KK", "FOLD"), Hoy, default);

        Assert.False(v!.Acerto);
        Assert.Equal("ALL-IN", v.AccionCorrecta);
        Assert.NotNull(v.Ficha);
        Assert.Equal("KK", v.Ficha.Mano);
        Assert.Equal(Hoy, v.Vence);
        Assert.Equal(0, progreso.Filas.Single().AciertosSeguidos);
    }

    /// <summary>
    /// Una mano mixta cuenta por cualquiera de sus partes: elegir una como "la
    /// correcta" sería inventar una estrategia que la tabla no declara.
    /// </summary>
    [Theory]
    [InlineData("ALL-IN")]
    [InlineData("CALL")]
    public async Task Una_mano_mixta_acepta_las_dos_partes(string accion)
    {
        var (handler, _) = Armar();

        var v = await handler.ResponderAsync(1, Enviada("AA", accion), Hoy, default);

        Assert.True(v!.Acerto);
        Assert.NotNull(v.Mix);
        Assert.Equal(2, v.Mix.Count);
    }

    [Fact]
    public async Task Una_accion_que_no_es_del_mix_falla()
    {
        var (handler, _) = Armar();

        var v = await handler.ResponderAsync(1, Enviada("AA", "FOLD"), Hoy, default);

        Assert.False(v!.Acerto);
    }

    /// <summary>
    /// Acertar dos veces seguidas sube dos escalones: el handler tiene que
    /// leer el progreso previo, no arrancar de cero cada vez.
    /// </summary>
    [Fact]
    public async Task Dos_aciertos_seguidos_suben_dos_escalones()
    {
        var (handler, _) = Armar();

        await handler.ResponderAsync(1, Enviada("KK", "ALL-IN"), Hoy, default);
        var v = await handler.ResponderAsync(1, Enviada("KK", "ALL-IN"), Hoy, default);

        Assert.Equal(Hoy.AddDays(3), v!.Vence);
    }

    [Fact]
    public async Task Una_casilla_que_no_existe_devuelve_null()
    {
        var (handler, _) = Armar();

        var v = await handler.ResponderAsync(
            1, new RespuestaEnviada("NO_EXISTE", "9-11bb", "SB_OR", "KK", "FOLD"), Hoy, default);

        Assert.Null(v);
    }
}
```

- [ ] **Step 2: Correr y verificar que falla**

Run: `dotnet test PokerProOS.slnx -p:SaltearFrontend=true --filter "FullyQualifiedName~ResponderRespuestaHandlerTests"`
Expected: FAIL — `error CS0246: no se encontró el tipo 'ResponderRespuestaHandler'`.

- [ ] **Step 3: `Partir` en la matriz, y que el intérprete use esa**

En `src/PokerProOS.Domain/Manos/MatrizDeManos.cs`, agregar dentro de la clase:

```csharp
    /// <summary>
    /// La etiqueta de una mano en sus dos rangos y su palo. Un par ("AA") no
    /// lleva palo: son dos cartas del mismo rango y no hay suited ni offsuit
    /// que elegir.
    ///
    /// Vive acá porque lo necesitan dos capas: el intérprete de voz, para una
    /// mano guardada entera, y el entrenador, para volver de la etiqueta a una
    /// ConsultaDeMano. Dos copias se despegarían.
    /// </summary>
    public static (string Alto, string Bajo, string? Palo) Partir(string etiqueta)
        => (etiqueta[..1], etiqueta.Substring(1, 1), etiqueta.Length > 2 ? etiqueta[2..] : null);
```

En `src/PokerProOS.Application/Voz/InterpretadorDeTexto.cs`, borrar el método privado `Partir` con su comentario y reemplazar su única llamada:

```csharp
        if (mano is not null) (rangoAlto, rangoBajo, paloResuelto) = Partir(mano);
```

por:

```csharp
        if (mano is not null) (rangoAlto, rangoBajo, paloResuelto) = MatrizDeManos.Partir(mano);
```

Agregar arriba el `using` si no está:

```csharp
using PokerProOS.Domain.Manos;
```

- [ ] **Step 4: El handler que arma la tanda**

Crear `src/PokerProOS.Application/Entrenador/ArmarTandaHandler.cs`:

```csharp
using PokerProOS.Domain.Entrenador;

namespace PokerProOS.Application.Entrenador;

/// <summary>
/// Junta el puerto y el planificador: lee lo vencido y lo ya conocido, y le
/// pide la tanda al planificador, que es donde vive la regla.
/// </summary>
public sealed class ArmarTandaHandler(
    IProgresoDeEntrenamiento progreso,
    PlanificadorDeTanda planificador)
{
    public async Task<IReadOnlyList<PreguntaDeTanda>> ArmarAsync(
        int usuarioId, FiltroDeTanda filtro, int tamano, DateOnly hoy, CancellationToken ct)
    {
        var vencidas = await progreso.VencidasAsync(usuarioId, hoy, ct);
        var todas = await progreso.TodasAsync(usuarioId, ct);

        return planificador.Planificar(
            vencidas,
            todas.Select(t => t.ClaveDeCasilla()).ToList(),
            filtro,
            tamano);
    }
}
```

- [ ] **Step 5: El handler que juzga la respuesta**

Crear `src/PokerProOS.Application/Entrenador/ResponderRespuestaHandler.cs`:

```csharp
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
```

- [ ] **Step 6: Correr y verificar que pasa**

Run: `dotnet test PokerProOS.slnx -p:SaltearFrontend=true --filter "FullyQualifiedName~ResponderRespuestaHandlerTests"`
Expected: PASS, 7 pruebas.

- [ ] **Step 7: La suite entera, porque se tocó el intérprete**

Run: `dotnet test PokerProOS.slnx -p:SaltearFrontend=true`
Expected: PASS, 274 pruebas.

- [ ] **Step 8: Commit**

```bash
git add src/PokerProOS.Domain/Manos src/PokerProOS.Application tests/PokerProOS.Tests/Entrenador
git commit -m "feat: los handlers del entrenador, con la mano mixta contando por sus dos partes"
```

---

### Task 6: Los endpoints

**Files:**
- Create: `src/PokerProOS.Api/Controllers/EntrenadorController.cs`
- Modify: `src/PokerProOS.Api/Program.cs`
- Test: `tests/PokerProOS.Tests/Entrenador/EntrenadorControllerTests.cs`

**Interfaces:**
- Consumes: `ArmarTandaHandler`, `ResponderRespuestaHandler`, `ICatalogoDeTablas`, `IRegistroDeAcciones`.
- Produces:
  - `POST /api/entrenador/tanda` — cuerpo `{ formato, situacion, minBB, maxBB, spot, tamano }` → `PreguntaDeTanda[]`
  - `POST /api/entrenador/respuesta` — cuerpo `RespuestaEnviada` → `VeredictoDeRespuesta`
  - `GET /api/entrenador/acciones?situacion=&stack=&spot=` → las acciones del spot, ordenadas, para los botones

- [ ] **Step 1: Escribir la prueba que falla**

Crear `tests/PokerProOS.Tests/Entrenador/EntrenadorControllerTests.cs`:

```csharp
using Microsoft.AspNetCore.Mvc;
using PokerProOS.Api.Controllers;
using PokerProOS.Application.Entrenador;
using PokerProOS.Application.Tablas;
using PokerProOS.Domain.Entrenador;
using PokerProOS.Domain.Manos;
using PokerProOS.Domain.Tablas;
using PokerProOS.Infrastructure.Tablas;

namespace PokerProOS.Tests.Entrenador;

public class EntrenadorControllerTests
{
    private sealed class ProgresoEnMemoria : IProgresoDeEntrenamiento
    {
        public List<ProgresoDeCasilla> Filas { get; } = [];

        public Task<IReadOnlyList<ProgresoDeCasilla>> VencidasAsync(
            int usuarioId, DateOnly hoy, CancellationToken ct)
            => Task.FromResult<IReadOnlyList<ProgresoDeCasilla>>(
                Filas.Where(f => f.Vence <= hoy).ToList());

        public Task<IReadOnlyList<ProgresoDeCasilla>> TodasAsync(int usuarioId, CancellationToken ct)
            => Task.FromResult<IReadOnlyList<ProgresoDeCasilla>>(Filas);

        public Task<ProgresoDeCasilla?> BuscarAsync(
            int usuarioId, string situacion, string claveDeStack, string spot, string mano,
            CancellationToken ct)
            => Task.FromResult(Filas.FirstOrDefault(f => f.Mano == mano && f.Spot == spot));

        public Task GuardarAsync(ProgresoDeCasilla progreso, CancellationToken ct)
        {
            if (!Filas.Contains(progreso)) Filas.Add(progreso);
            return Task.CompletedTask;
        }
    }

    private static EntrenadorController Armar()
    {
        var acciones = RegistroDeAccionesJson.Cargar(Rutas.Registro("acciones.json"));
        var catalogo = new CargadorDeTablas(new ValidadorDeTabla(acciones), acciones)
            .CargarDirectorio(Rutas.SemillasDeTablas);
        var progreso = new ProgresoEnMemoria();

        return new EntrenadorController(
            new ArmarTandaHandler(progreso, new PlanificadorDeTanda(catalogo)),
            new ResponderRespuestaHandler(
                new ResolverManoHandler(catalogo),
                new AnalizadorDeMemoria(catalogo),
                catalogo,
                progreso),
            catalogo,
            acciones);
    }

    private static T Cuerpo<T>(IActionResult resultado)
        => Assert.IsType<T>(Assert.IsType<OkObjectResult>(resultado).Value);

    [Fact]
    public async Task La_tanda_devuelve_el_tamano_pedido()
    {
        var preguntas = Cuerpo<IReadOnlyList<PreguntaDeTanda>>(
            await Armar().Tanda(new TandaPedida(null, null, null, null, null, 5), default));

        Assert.Equal(5, preguntas.Count);
    }

    /// <summary>
    /// Un tamaño absurdo no puede hacer que el servidor arme una tanda de un
    /// millón de preguntas: se recorta antes de planificar.
    /// </summary>
    [Fact]
    public async Task Un_tamano_fuera_de_rango_se_recorta()
    {
        var preguntas = Cuerpo<IReadOnlyList<PreguntaDeTanda>>(
            await Armar().Tanda(new TandaPedida(null, null, null, null, null, 5000), default));

        Assert.Equal(EntrenadorController.TamanoMaximo, preguntas.Count);
    }

    [Fact]
    public async Task Responder_una_casilla_inexistente_da_404()
    {
        var resultado = await Armar().Responder(
            new RespuestaEnviada("NO_EXISTE", "1-5bb", "SB_OR", "AA", "FOLD"), default);

        Assert.IsType<NotFoundObjectResult>(resultado);
    }

    /// <summary>
    /// Los botones salen del spot, no de una lista en código, y traen el color
    /// y el orden del registro: es la misma memoria visual que el usuario ya
    /// entrenó mirando las grillas.
    /// </summary>
    [Fact]
    public void Las_acciones_de_un_spot_salen_del_spot_con_su_color()
    {
        var acciones = Cuerpo<IReadOnlyList<AccionDefinida>>(
            Armar().Acciones("HU_SB_OR_FISH", "1-4bb", "SB_OR"));

        Assert.NotEmpty(acciones);
        Assert.All(acciones, a => Assert.StartsWith("#", a.Color));
        Assert.Equal(acciones.OrderBy(a => a.Orden), acciones);
    }

    [Fact]
    public void Las_acciones_de_un_spot_inexistente_dan_404()
        => Assert.IsType<NotFoundObjectResult>(
            Armar().Acciones("HU_SB_OR_FISH", "1-4bb", "NO_EXISTE"));
}
```

- [ ] **Step 2: Correr y verificar que falla**

Run: `dotnet test PokerProOS.slnx -p:SaltearFrontend=true --filter "FullyQualifiedName~EntrenadorControllerTests"`
Expected: FAIL — `error CS0246: no se encontró el tipo 'EntrenadorController'`.

- [ ] **Step 3: El controlador**

Crear `src/PokerProOS.Api/Controllers/EntrenadorController.cs`:

```csharp
using Microsoft.AspNetCore.Mvc;
using PokerProOS.Application.Entrenador;
using PokerProOS.Application.Tablas;

namespace PokerProOS.Api.Controllers;

/// <summary>Lo que la pantalla pide para arrancar una tanda.</summary>
public record TandaPedida(
    string? Formato, string? Situacion, decimal? MinBB, decimal? MaxBB, string? Spot,
    int Tamano = 20);

[ApiController]
[Route("api/entrenador")]
public sealed class EntrenadorController(
    ArmarTandaHandler armar,
    ResponderRespuestaHandler responder,
    ICatalogoDeTablas catalogo,
    IRegistroDeAcciones acciones) : ControllerBase
{
    /// <summary>
    /// El techo de una tanda. Sin esto, un cuerpo con `tamano: 5000000` haría
    /// que el planificador recorra las 57.000 casillas y arme una respuesta
    /// enorme, sin que nadie lo haya pedido de verdad.
    /// </summary>
    public const int TamanoMaximo = 100;

    /// <summary>
    /// Quién está entrenando. El spec pide que el usuario sea parte de la
    /// clave del progreso desde el primer día pero no construye login: este es
    /// EL único lugar donde se decide, para que agregar identidad sea cambiar
    /// de dónde sale este número y nada más.
    /// </summary>
    private static int UsuarioActual => 1;

    /// <summary>
    /// El hoy del calendario. Los handlers no tienen reloj propio —así se
    /// prueban sin depender del día en que corren—, y quien lo tiene es el
    /// borde, que es acá.
    /// </summary>
    private static DateOnly Hoy => DateOnly.FromDateTime(DateTime.Now);

    [HttpPost("tanda")]
    public async Task<IActionResult> Tanda([FromBody] TandaPedida pedida, CancellationToken ct)
    {
        var tamano = Math.Clamp(pedida.Tamano, 1, TamanoMaximo);
        var filtro = new FiltroDeTanda(
            pedida.Formato, pedida.Situacion, pedida.MinBB, pedida.MaxBB, pedida.Spot);

        var preguntas = await armar.ArmarAsync(UsuarioActual, filtro, tamano, Hoy, ct);
        return Ok(preguntas);
    }

    [HttpPost("respuesta")]
    public async Task<IActionResult> Responder(
        [FromBody] RespuestaEnviada respuesta, CancellationToken ct)
    {
        var veredicto = await responder.ResponderAsync(UsuarioActual, respuesta, Hoy, ct);

        // La tabla pudo haberse corregido entre que se armo la tanda y se
        // contesto. No es un error del usuario: la pantalla saltea la pregunta.
        return veredicto is null
            ? NotFound(new { error = "Esa casilla ya no existe en el catálogo." })
            : Ok(veredicto);
    }

    /// <summary>
    /// Las acciones que ese spot usa de verdad, con su color y su orden del
    /// registro. Salen del spot y no de una lista en código: si la grilla
    /// pinta ALL-IN de verde, el botón es verde, y romper esa memoria visual
    /// sería entrenar dos cosas distintas.
    /// </summary>
    [HttpGet("acciones")]
    public IActionResult Acciones(
        [FromQuery] string situacion, [FromQuery] string stack, [FromQuery] string spot)
    {
        var tabla = catalogo.Spot(situacion, stack, spot);
        if (tabla is null)
            return NotFound(new { error = "Ese spot no existe." });

        var delSpot = tabla.Conteos.Keys
            .Where(acciones.Existe)
            .Select(acciones.Obtener)
            .OrderBy(a => a.Orden)
            .ToList();

        return Ok((IReadOnlyList<AccionDefinida>)delSpot);
    }
}
```

- [ ] **Step 4: Correr y verificar que pasa**

Run: `dotnet test PokerProOS.slnx -p:SaltearFrontend=true --filter "FullyQualifiedName~EntrenadorControllerTests"`
Expected: PASS, 5 pruebas.

- [ ] **Step 5: Registrar todo en `Program.cs`**

En `src/PokerProOS.Api/Program.cs`, agregar los `using`:

```csharp
using PokerProOS.Application.Entrenador;
using PokerProOS.Infrastructure.Entrenador;
```

Después de `builder.Services.AddSingleton<AnalizadorDeMemoria>();` agregar:

```csharp
builder.Services.AddSingleton<PlanificadorDeTanda>();
```

Y junto a los demás `AddScoped` (después de `IRepositorioDeDiario`):

```csharp
// Scoped como el resto de lo que toca la base: el DbContext lo es.
builder.Services.AddScoped<IProgresoDeEntrenamiento, ProgresoDeEntrenamientoSql>();
builder.Services.AddScoped<ArmarTandaHandler>();
builder.Services.AddScoped<ResponderRespuestaHandler>();
```

- [ ] **Step 6: Compilar y correr la suite entera**

Run: `dotnet build PokerProOS.slnx -p:SaltearFrontend=true && dotnet test PokerProOS.slnx -p:SaltearFrontend=true`
Expected: `Compilación correcta` y PASS, 279 pruebas.

- [ ] **Step 7: Commit**

```bash
git add src/PokerProOS.Api tests/PokerProOS.Tests/Entrenador/EntrenadorControllerTests.cs
git commit -m "feat: los endpoints del entrenador, con el usuario resuelto en un solo lugar"
```

---

### Task 7: El cliente y la mesa

> **Nota sobre pruebas de frontend.** El proyecto no tiene runner de tests de
> React: `package.json` solo declara `dev`, `build` y `lint`. Las tareas 7 a 9
> se verifican con `npx tsc -b` (tipos), `npx oxlint src` (lint) y una pasada
> por el navegador. No inventar Vitest ni Jest: eso es una decisión aparte.

**Files:**
- Modify: `frontend/src/core/models/catalogo.model.ts`
- Create: `frontend/src/core/services/entrenadorApi.ts`
- Create: `frontend/src/features/entrenador/MesaSimulada.tsx`
- Modify: `frontend/src/index.css`

**Interfaces:**
- Consumes: los endpoints de la Task 6.
- Produces: los tipos `PreguntaDeTanda`, `VeredictoDeRespuesta`, `TandaPedida`, `RespuestaEnviada`; las funciones `pedirTanda`, `responder`, `accionesDelSpot`; el componente `<MesaSimulada pregunta={...} />`.

- [ ] **Step 1: Los tipos**

En `frontend/src/core/models/catalogo.model.ts`, agregar al final:

```typescript
/* ---------- Entrenador ---------- */

export interface PreguntaDeTanda {
  situacion: string
  etiquetaDeSituacion: string
  claveDeStack: string
  spot: string
  etiquetaDeSpot: string
  mano: string
  /** Material nuevo, sin progreso previo. */
  esNueva: boolean
}

export interface TandaPedida {
  formato: string | null
  situacion: string | null
  minBB: number | null
  maxBB: number | null
  spot: string | null
  tamano: number
}

export interface RespuestaEnviada {
  situacion: string
  claveDeStack: string
  spot: string
  mano: string
  accion: string
}

export interface VeredictoDeRespuesta {
  acerto: boolean
  accionCorrecta: string
  mix: ParteDeMix[] | null
  /** Solo al fallar: acertar sigue de largo. */
  ficha: FichaDeMemoria | null
  /** Cuándo vuelve a preguntarse esta casilla. */
  vence: string
}
```

- [ ] **Step 2: El cliente**

Crear `frontend/src/core/services/entrenadorApi.ts`:

```typescript
import type {
  AccionDefinida, PreguntaDeTanda, RespuestaEnviada, TandaPedida, VeredictoDeRespuesta,
} from '../models/catalogo.model'

/**
 * El entrenador es lo único de la app que NO anda sin base de datos: un
 * calendario de repetición que pierde respuestas no es un calendario. Por eso
 * los errores se propagan en vez de tragarse — la pantalla los muestra.
 */
async function pedir<T>(url: string, metodo: string, cuerpo?: unknown): Promise<T> {
  const respuesta = await fetch(url, {
    method: metodo,
    headers: cuerpo ? { 'Content-Type': 'application/json' } : undefined,
    body: cuerpo ? JSON.stringify(cuerpo) : undefined,
  })
  if (!respuesta.ok) {
    const error = await respuesta.json().catch(() => null) as { error?: string } | null
    throw new Error(error?.error ?? `${respuesta.status} ${respuesta.statusText}`)
  }
  return respuesta.json() as Promise<T>
}

export const pedirTanda = (pedida: TandaPedida) =>
  pedir<PreguntaDeTanda[]>('/api/entrenador/tanda', 'POST', pedida)

export const responder = (respuesta: RespuestaEnviada) =>
  pedir<VeredictoDeRespuesta>('/api/entrenador/respuesta', 'POST', respuesta)

export const accionesDelSpot = (situacion: string, stack: string, spot: string) =>
  pedir<AccionDefinida[]>(
    `/api/entrenador/acciones?situacion=${encodeURIComponent(situacion)}`
    + `&stack=${encodeURIComponent(stack)}&spot=${encodeURIComponent(spot)}`,
    'GET')
```

- [ ] **Step 3: La mesa**

Crear `frontend/src/features/entrenador/MesaSimulada.tsx`:

```tsx
import type { PreguntaDeTanda } from '../../core/models/catalogo.model'

interface Props {
  pregunta: PreguntaDeTanda
}

/** Los símbolos de los dos palos que una casilla puede representar. */
const PALOS = { s: '♠', o: '♦' } as const

/**
 * La mesa de la pregunta: las dos cartas grandes, la banda de stack y dónde
 * estás.
 *
 * Muestra lo que los datos sostienen y nada más. El spec pedía además el bote
 * y las fichas del rival, y ninguna tabla los declara: calcularlos por tipo de
 * spot sería deducirlos de la clave, que es justo lo que el proyecto no hace.
 * La etiqueta de la situación ya trae el rival ("BB vs limp | fish"), así que
 * se muestra tal cual en vez de partirla.
 *
 * Una casilla suited se dibuja con dos picas y una offsuit con pica y diamante:
 * el palo concreto no importa —la tabla razona por casilla, no por combo— pero
 * verlo en colores distintos es lo que hace leer "offsuit" de un vistazo.
 */
export function MesaSimulada({ pregunta }: Props) {
  const [alto, bajo] = [pregunta.mano[0], pregunta.mano[1]]
  const palo = pregunta.mano.length > 2 ? pregunta.mano[2] : null
  const segundoPalo = palo === 's' ? PALOS.s : PALOS.o

  return (
    <section className="mesa">
      <p className="mesa-donde">
        {pregunta.etiquetaDeSituacion} · {pregunta.claveDeStack} · {pregunta.etiquetaDeSpot}
        {pregunta.esNueva && <span className="mesa-nueva">nueva</span>}
      </p>

      <div className="mesa-cartas">
        <span className="carta carta-negra">
          <strong>{alto}</strong><em>{PALOS.s}</em>
        </span>
        <span className={`carta ${palo === 's' ? 'carta-negra' : 'carta-roja'}`}>
          <strong>{bajo}</strong><em>{segundoPalo}</em>
        </span>
      </div>

      <p className="mesa-mano">{pregunta.mano}</p>
    </section>
  )
}
```

- [ ] **Step 4: Los estilos**

En `frontend/src/index.css`, antes de `.entrenamiento-cuerpo {`, agregar:

```css
/* ---------- Entrenador ---------- */

.mesa {
  display: grid; gap: 14px; justify-items: center;
  padding: 26px 20px;
  border: 1px solid var(--borde); border-radius: 12px; background: var(--panel);
}
.mesa-donde {
  display: flex; align-items: center; gap: 8px;
  margin: 0; color: var(--apagado); font-size: 13px;
}
.mesa-nueva {
  padding: 2px 8px; border-radius: 99px;
  background: var(--panel-2); color: var(--acento);
  font-size: 10px; text-transform: uppercase; letter-spacing: .09em;
}
.mesa-cartas { display: flex; gap: 12px; }
.carta {
  display: grid; justify-items: center; gap: 2px;
  width: 74px; padding: 12px 0;
  border-radius: 9px; background: #f3f5f8;
  font-variant-numeric: tabular-nums;
}
.carta strong { font-size: 34px; line-height: 1; }
.carta em { font-size: 20px; font-style: normal; line-height: 1; }
.carta-negra { color: #14181f; }
.carta-roja { color: #b3261e; }
.mesa-mano {
  margin: 0; color: var(--apagado);
  font-size: 12px; letter-spacing: .12em; text-transform: uppercase;
}
```

- [ ] **Step 5: Verificar**

Run: `cd frontend && npx tsc -b && npx oxlint src`
Expected: las dos sin salida (limpio).

- [ ] **Step 6: Commit**

```bash
git add frontend/src/core frontend/src/features/entrenador frontend/src/index.css
git commit -m "feat: la mesa del entrenador y su cliente de API"
```

---

### Task 8: Los botones y el veredicto

**Files:**
- Create: `frontend/src/features/entrenador/BotonesDeAccion.tsx`
- Create: `frontend/src/features/entrenador/Veredicto.tsx`
- Modify: `frontend/src/index.css`

**Interfaces:**
- Consumes: `AccionDefinida`, `VeredictoDeRespuesta` (Task 7), `FichaDeMemoria.tsx` (ya existe).
- Produces: `<BotonesDeAccion acciones={} deshabilitado={} onElegir={} />` y `<Veredicto veredicto={} acciones={} onSeguir={} />`.

- [ ] **Step 1: Los botones**

Crear `frontend/src/features/entrenador/BotonesDeAccion.tsx`:

```tsx
import { useEffect } from 'react'
import type { AccionDefinida } from '../../core/models/catalogo.model'

interface Props {
  acciones: AccionDefinida[]
  deshabilitado: boolean
  onElegir: (clave: string) => void
}

/**
 * Los botones del spot, con el color del registro de acciones.
 *
 * El color no es decorativo: es la misma memoria visual que se entrenó
 * mirando las grillas, y pintar ALL-IN de otro color acá sería entrenar dos
 * cosas distintas. El atajo de teclado sale del campo `orden` del registro,
 * así que la tecla 1 es siempre la misma acción en toda la app.
 */
export function BotonesDeAccion({ acciones, deshabilitado, onElegir }: Props) {
  useEffect(() => {
    if (deshabilitado) return
    const alTeclear = (evento: KeyboardEvent) => {
      const indice = Number(evento.key) - 1
      const accion = acciones[indice]
      if (!Number.isNaN(indice) && accion) onElegir(accion.clave)
    }
    window.addEventListener('keydown', alTeclear)
    return () => window.removeEventListener('keydown', alTeclear)
  }, [acciones, deshabilitado, onElegir])

  return (
    <div className="botones-accion">
      {acciones.map((accion, indice) => (
        <button
          key={accion.clave}
          type="button"
          className="boton-accion"
          disabled={deshabilitado}
          style={{ background: accion.color, color: accion.colorTexto }}
          onClick={() => onElegir(accion.clave)}
        >
          <span className="boton-accion-tecla">{indice + 1}</span>
          {accion.etiqueta}
        </button>
      ))}
    </div>
  )
}
```

- [ ] **Step 2: El veredicto**

Crear `frontend/src/features/entrenador/Veredicto.tsx`:

```tsx
import type { AccionDefinida, VeredictoDeRespuesta } from '../../core/models/catalogo.model'
import { FichaDeMemoria } from '../tablas/FichaDeMemoria'

interface Props {
  veredicto: VeredictoDeRespuesta
  acciones: AccionDefinida[]
  onSeguir: () => void
}

/**
 * Qué pasó con la respuesta.
 *
 * Al acertar es una línea y seguís. Al fallar viene la ficha entera —el bloque
 * de la familia, el umbral de stack, las emparentadas, el peso en combos y el
 * tip— porque el momento en que más entra una explicación es justo el que un
 * "incorrecto" seco desaprovecha. No hay lógica nueva: es el mismo componente
 * que el popup de la grilla, en el momento en que más sirve.
 *
 * El tip no se edita desde acá: entrenando no se corrigen tablas, y abrir esa
 * puerta en medio de una tanda invita a "arreglar" la tabla en vez de aprenderla.
 */
export function Veredicto({ veredicto, acciones, onSeguir }: Props) {
  const correcta = acciones.find((a) => a.clave === veredicto.accionCorrecta)

  return (
    <section className={`veredicto ${veredicto.acerto ? 'veredicto-bien' : 'veredicto-mal'}`}>
      <header className="veredicto-cabecera">
        <strong>{veredicto.acerto ? 'Bien' : 'No'}</strong>
        <span
          className="veredicto-accion"
          style={correcta ? { background: correcta.color, color: correcta.colorTexto } : undefined}
        >
          {correcta?.etiqueta ?? veredicto.accionCorrecta}
        </span>
        {veredicto.mix && veredicto.mix.length > 1 && (
          <span className="veredicto-mix">
            mix · {veredicto.mix.map((p) => `${p.frecuencia}% ${p.accion}`).join(' / ')}
          </span>
        )}
        <button type="button" className="boton-principal" onClick={onSeguir}>
          Seguir
        </button>
      </header>

      {veredicto.ficha && (
        <FichaDeMemoria
          ficha={veredicto.ficha}
          acciones={acciones}
          guardandoTip={false}
          errorAlGuardarTip={null}
          onGuardarTip={() => {}}
          onCerrar={onSeguir}
        />
      )}
    </section>
  )
}
```

- [ ] **Step 3: Los estilos**

En `frontend/src/index.css`, después del bloque `.mesa-mano { … }`, agregar:

```css
.botones-accion { display: flex; gap: 10px; flex-wrap: wrap; justify-content: center; }
.boton-accion {
  display: inline-flex; align-items: center; gap: 8px;
  padding: 11px 18px;
  border: none; border-radius: 8px;
  font-size: 14px; font-weight: 800; letter-spacing: .03em;
  font-family: inherit; cursor: pointer;
}
.boton-accion:disabled { opacity: .45; cursor: default; }
.boton-accion-tecla {
  padding: 1px 6px; border-radius: 4px;
  background: rgba(0, 0, 0, .28);
  font-size: 11px; font-weight: 700;
}

.veredicto { display: grid; gap: 14px; margin-top: 16px; }
.veredicto-cabecera {
  display: flex; align-items: center; gap: 12px; flex-wrap: wrap;
  padding: 12px 14px; border-radius: 9px; border: 1px solid var(--borde);
}
.veredicto-bien .veredicto-cabecera { border-color: #2f6b3a; background: #122416; }
.veredicto-mal .veredicto-cabecera { border-color: #7a4a2a; background: #2a1b12; }
.veredicto-cabecera strong { font-size: 17px; }
.veredicto-accion {
  padding: 5px 11px; border-radius: 6px;
  font-size: 13px; font-weight: 800; letter-spacing: .03em;
}
.veredicto-mix { color: var(--apagado); font-size: 12px; }
.veredicto-cabecera .boton-principal { margin-left: auto; }
```

- [ ] **Step 4: Verificar**

Run: `cd frontend && npx tsc -b && npx oxlint src`
Expected: las dos sin salida.

- [ ] **Step 5: Commit**

```bash
git add frontend/src/features/entrenador frontend/src/index.css
git commit -m "feat: los botones con el color del registro y el veredicto con la ficha"
```

---

### Task 9: La pantalla que junta todo

**Files:**
- Create: `frontend/src/features/entrenador/FiltroDeTanda.tsx`
- Create: `frontend/src/features/entrenador/PaginaDeEntrenador.tsx`
- Modify: `frontend/src/App.tsx`

**Interfaces:**
- Consumes: todo lo de las Tasks 7 y 8, `useCatalogo` (ya existe).
- Produces: `<PaginaDeEntrenador />`, registrado como módulo `entrenador` del grupo `Spins`.

- [ ] **Step 1: El filtro**

Crear `frontend/src/features/entrenador/FiltroDeTanda.tsx`:

```tsx
import type { SituacionResumen, TandaPedida } from '../../core/models/catalogo.model'

interface Props {
  situaciones: SituacionResumen[]
  pedida: TandaPedida
  onCambiar: (pedida: TandaPedida) => void
  onArrancar: () => void
  cargando: boolean
}

/** Los tamaños de tanda que se ofrecen. El del spec, 20, va primero. */
const TAMANOS = [20, 10, 40, 60]

/**
 * Sobre qué entrenar. Todo sale del catálogo: los formatos son los que los
 * archivos declaran y las situaciones se acotan al formato elegido, igual que
 * en los selectores de la grilla.
 *
 * El rango de stack va en BB y se compara contra la cobertura real de cada
 * tabla, no contra su clave: pedir de 7 a 12 trae toda tabla cuya banda toque
 * ese tramo.
 */
export function FiltroDeTanda({
  situaciones, pedida, onCambiar, onArrancar, cargando,
}: Props) {
  const formatos = [...new Set(situaciones.map((s) => s.formato))]
  const delFormato = pedida.formato
    ? situaciones.filter((s) => s.formato === pedida.formato)
    : situaciones

  return (
    <div className="filtro-tanda">
      <label>
        Formato
        <select
          value={pedida.formato ?? ''}
          onChange={(e) =>
            onCambiar({ ...pedida, formato: e.target.value || null, situacion: null })}
        >
          <option value="">Todos</option>
          {formatos.map((f) => <option key={f} value={f}>{f}</option>)}
        </select>
      </label>

      <label>
        Situación
        <select
          value={pedida.situacion ?? ''}
          onChange={(e) => onCambiar({ ...pedida, situacion: e.target.value || null })}
        >
          <option value="">Todas</option>
          {delFormato.map((s) => (
            <option key={s.clave} value={s.clave}>{s.etiqueta}</option>
          ))}
        </select>
      </label>

      <label>
        Stack desde
        <input
          type="number" min={1} max={200} inputMode="numeric"
          value={pedida.minBB ?? ''}
          onChange={(e) =>
            onCambiar({ ...pedida, minBB: e.target.value ? Number(e.target.value) : null })}
        />
      </label>

      <label>
        hasta
        <input
          type="number" min={1} max={200} inputMode="numeric"
          value={pedida.maxBB ?? ''}
          onChange={(e) =>
            onCambiar({ ...pedida, maxBB: e.target.value ? Number(e.target.value) : null })}
        />
      </label>

      <label>
        Manos
        <select
          value={pedida.tamano}
          onChange={(e) => onCambiar({ ...pedida, tamano: Number(e.target.value) })}
        >
          {TAMANOS.map((t) => <option key={t} value={t}>{t}</option>)}
        </select>
      </label>

      <button
        type="button" className="boton-principal"
        disabled={cargando} onClick={onArrancar}
      >
        {cargando ? 'Armando…' : 'Arrancar'}
      </button>
    </div>
  )
}
```

- [ ] **Step 2: La página**

Crear `frontend/src/features/entrenador/PaginaDeEntrenador.tsx`:

```tsx
import { useEffect, useState } from 'react'
import { useCatalogo } from '../../core/hooks/useCatalogo'
import type {
  AccionDefinida, PreguntaDeTanda, TandaPedida, VeredictoDeRespuesta,
} from '../../core/models/catalogo.model'
import { accionesDelSpot, pedirTanda, responder } from '../../core/services/entrenadorApi'
import { BotonesDeAccion } from './BotonesDeAccion'
import { FiltroDeTanda } from './FiltroDeTanda'
import { MesaSimulada } from './MesaSimulada'
import { Veredicto } from './Veredicto'

const PEDIDA_INICIAL: TandaPedida = {
  formato: null, situacion: null, minBB: null, maxBB: null, spot: null, tamano: 20,
}

/**
 * El bucle del entrenador: filtro → tanda → pregunta → veredicto → siguiente.
 *
 * A diferencia del resto de la app, esto NO anda sin base de datos: un
 * calendario de repetición que pierde respuestas no es un calendario. Por eso
 * el error se muestra en pantalla en lugar de tragarse, que es lo que hacen la
 * bitácora y el diario.
 */
export function PaginaDeEntrenador() {
  const { catalogo } = useCatalogo()

  const [pedida, setPedida] = useState<TandaPedida>(PEDIDA_INICIAL)
  const [tanda, setTanda] = useState<PreguntaDeTanda[] | null>(null)
  const [indice, setIndice] = useState(0)
  const [acciones, setAcciones] = useState<AccionDefinida[]>([])
  const [veredicto, setVeredicto] = useState<VeredictoDeRespuesta | null>(null)
  const [aciertos, setAciertos] = useState(0)
  const [cargando, setCargando] = useState(false)
  const [error, setError] = useState<string | null>(null)

  const pregunta = tanda?.[indice] ?? null

  // Los botones son los del spot de la pregunta, no una lista fija: cada spot
  // usa las acciones que usa.
  useEffect(() => {
    if (!pregunta) return
    let cancelado = false
    accionesDelSpot(pregunta.situacion, pregunta.claveDeStack, pregunta.spot)
      .then((a) => { if (!cancelado) setAcciones(a) })
      .catch(() => { if (!cancelado) setAcciones([]) })
    return () => { cancelado = true }
  }, [pregunta])

  const arrancar = async () => {
    setCargando(true)
    setError(null)
    try {
      const preguntas = await pedirTanda(pedida)
      setTanda(preguntas)
      setIndice(0)
      setAciertos(0)
      setVeredicto(null)
      if (preguntas.length === 0) setError('No hay nada para entrenar con ese filtro.')
    } catch (e) {
      setError(e instanceof Error ? e.message : 'No se pudo armar la tanda.')
    } finally {
      setCargando(false)
    }
  }

  const elegir = async (accion: string) => {
    if (!pregunta || veredicto) return
    try {
      const v = await responder({
        situacion: pregunta.situacion,
        claveDeStack: pregunta.claveDeStack,
        spot: pregunta.spot,
        mano: pregunta.mano,
        accion,
      })
      setVeredicto(v)
      if (v.acerto) setAciertos((previo) => previo + 1)
    } catch (e) {
      setError(e instanceof Error ? e.message : 'No se pudo guardar la respuesta.')
    }
  }

  const seguir = () => {
    setVeredicto(null)
    setIndice((previo) => previo + 1)
  }

  const terminada = tanda !== null && indice >= tanda.length

  return (
    <div className="entrenador">
      <header className="entrenamiento-cabecera">
        <div>
          <h1>Entrenador</h1>
          <p className="subtitulo">Te pregunta, y al fallar te explica</p>
        </div>
        {tanda && !terminada && (
          <p className="entrenador-marcador">
            {indice + 1} / {tanda.length} · {aciertos} bien
          </p>
        )}
      </header>

      {error && <p className="sin-entender-error">{error}</p>}

      {catalogo && (
        <FiltroDeTanda
          situaciones={catalogo.situaciones}
          pedida={pedida}
          onCambiar={setPedida}
          onArrancar={() => void arrancar()}
          cargando={cargando}
        />
      )}

      {terminada && tanda.length > 0 && (
        <p className="entrenador-final">
          Tanda terminada: {aciertos} de {tanda.length}.
        </p>
      )}

      {pregunta && (
        <>
          <MesaSimulada pregunta={pregunta} />
          <BotonesDeAccion
            acciones={acciones}
            deshabilitado={veredicto !== null}
            onElegir={(clave) => void elegir(clave)}
          />
          {veredicto && (
            <Veredicto veredicto={veredicto} acciones={acciones} onSeguir={seguir} />
          )}
        </>
      )}
    </div>
  )
}
```

- [ ] **Step 3: Los estilos que faltan**

En `frontend/src/index.css`, después del bloque `.veredicto-cabecera .boton-principal { … }`, agregar:

```css
.entrenador { display: grid; gap: 16px; }
.entrenador-marcador {
  margin: 0; color: var(--apagado);
  font-size: 13px; font-variant-numeric: tabular-nums;
}
.entrenador-final {
  margin: 0; padding: 12px 14px;
  border: 1px solid var(--borde); border-radius: 9px; background: var(--panel);
  font-size: 14px;
}
.filtro-tanda {
  display: flex; gap: 12px; align-items: flex-end; flex-wrap: wrap;
  padding: 12px 14px;
  border: 1px solid var(--borde); border-radius: 9px; background: var(--panel);
}
.filtro-tanda label {
  display: grid; gap: 4px;
  color: var(--apagado); font-size: 11px;
  text-transform: uppercase; letter-spacing: .09em;
}
.filtro-tanda select,
.filtro-tanda input {
  padding: 7px 9px;
  border: 1px solid var(--borde); border-radius: 6px;
  background: var(--panel-2); color: var(--texto);
  font-size: 13px; font-family: inherit;
  text-transform: none; letter-spacing: normal;
}
.filtro-tanda input { width: 82px; }
```

- [ ] **Step 4: Registrar el módulo**

En `frontend/src/App.tsx`, agregar el import:

```typescript
import { PaginaDeEntrenador } from './features/entrenador/PaginaDeEntrenador'
```

Y en el grupo `spins`, justo después del módulo `entrenamiento`, insertar:

```tsx
        {
          clave: 'entrenador',
          etiqueta: 'Entrenador',
          descripcion: 'Te pregunta y te corrige',
          disponible: true,
          contenido: <PaginaDeEntrenador />,
        },
```

- [ ] **Step 5: Verificar**

Run: `cd frontend && npx tsc -b && npx oxlint src`
Expected: las dos sin salida.

- [ ] **Step 6: Probarlo de verdad**

```bash
dotnet build PokerProOS.slnx
dotnet run --project src/PokerProOS.Api
```

En `http://localhost:5000`, entrar a **Spins › Entrenador**, arrancar una tanda
de 20 y comprobar: que salgan cartas, que los botones tengan el color de la
grilla, que las teclas 1..n contesten, que al fallar aparezca la ficha, y que
el marcador avance. Si SQL Server no está levantado, tiene que verse el error
en pantalla y no una pantalla en blanco.

- [ ] **Step 7: Commit**

```bash
git add frontend/src
git commit -m "feat: la pantalla del entrenador"
```

---

### Task 10: La voz — cantar la pregunta y escuchar la respuesta

**Files:**
- Create: `src/PokerProOS.Application/Entrenador/InterpretadorDeRespuesta.cs`
- Modify: `src/PokerProOS.Api/Controllers/EntrenadorController.cs`
- Modify: `src/PokerProOS.Api/Program.cs`
- Modify: `frontend/src/core/services/entrenadorApi.ts`
- Create: `frontend/src/features/entrenador/useCantarPregunta.ts`
- Modify: `frontend/src/features/entrenador/PaginaDeEntrenador.tsx`
- Test: `tests/PokerProOS.Tests/Entrenador/InterpretadorDeRespuestaTests.cs`

**Interfaces:**
- Consumes: `IRegistroDeAcciones`, `ResponderRespuestaHandler`.
- Produces:
  - `InterpretadorDeRespuesta.Interpretar(string texto) → string?` (la clave de acción, o null)
  - `POST /api/entrenador/respuesta-hablada` — cuerpo `{ situacion, claveDeStack, spot, mano, texto }`
  - `responderHablado(...)` en `entrenadorApi.ts`
  - `useCantarPregunta(pregunta, activo)` — dice el spot, el stack y la mano

- [ ] **Step 1: Escribir la prueba que falla**

Crear `tests/PokerProOS.Tests/Entrenador/InterpretadorDeRespuestaTests.cs`:

```csharp
using PokerProOS.Application.Entrenador;
using PokerProOS.Infrastructure.Tablas;

namespace PokerProOS.Tests.Entrenador;

/// <summary>
/// Entrenando, un dictado es una respuesta y no una consulta. Las formas
/// salen de los `dichos` de acciones.json —las 15 acciones los tienen—, así
/// que agregar una manera de decir "all in" no toca código.
/// </summary>
public class InterpretadorDeRespuestaTests
{
    private static InterpretadorDeRespuesta Armar() =>
        new(RegistroDeAccionesJson.Cargar(Rutas.Registro("acciones.json")));

    [Theory]
    [InlineData("all in", "ALL-IN")]
    [InlineData("shove", "ALL-IN")]
    [InlineData("ALL IN.", "ALL-IN")]
    public void Reconoce_las_formas_del_registro(string texto, string esperada)
        => Assert.Equal(esperada, Armar().Interpretar(texto));

    /// <summary>
    /// Gana la forma más larga. Sin eso, una acción cuyo dicho sea prefijo de
    /// otra se llevaría las dos, en silencio.
    /// </summary>
    [Fact]
    public void Gana_la_forma_mas_larga()
    {
        var acciones = RegistroDeAccionesJson.Cargar(Rutas.Registro("acciones.json"));
        var interprete = new InterpretadorDeRespuesta(acciones);

        foreach (var accion in acciones.Todas)
            foreach (var dicho in accion.Dichos)
                Assert.Equal(accion.Clave, interprete.Interpretar(dicho));
    }

    /// <summary>
    /// Lo que no es una respuesta no se adivina: entrenando, contestar por
    /// vos una acción que no dijiste te ensucia el calendario.
    /// </summary>
    [Theory]
    [InlineData("")]
    [InlineData("pasame la sal")]
    public void Lo_que_no_es_una_accion_devuelve_null(string texto)
        => Assert.Null(Armar().Interpretar(texto));
}
```

- [ ] **Step 2: Correr y verificar que falla**

Run: `dotnet test PokerProOS.slnx -p:SaltearFrontend=true --filter "FullyQualifiedName~InterpretadorDeRespuestaTests"`
Expected: FAIL — `error CS0246: no se encontró el tipo 'InterpretadorDeRespuesta'`.

- [ ] **Step 3: Implementar**

Crear `src/PokerProOS.Application/Entrenador/InterpretadorDeRespuesta.cs`:

```csharp
using System.Globalization;
using System.Text;
using PokerProOS.Application.Tablas;

namespace PokerProOS.Application.Entrenador;

/// <summary>
/// El texto que oyó el navegador, entendido como una respuesta del
/// entrenamiento.
///
/// Es su propia pieza y no un modo de <c>InterpretadorDeTexto</c> a propósito:
/// no hace falta estado. La pantalla de entrenamiento manda su texto a su
/// endpoint, y quién sabe el modo es la pantalla, que ya lo sabe. Un flag
/// global de "estoy entrenando" es una variable más que puede quedar mal.
///
/// Las formas salen de los `dichos` de acciones.json, igual que todo lo demás
/// del proyecto: agregar una manera de decir "all in" no toca código.
/// </summary>
public sealed class InterpretadorDeRespuesta(IRegistroDeAcciones acciones)
{
    /// <summary>La clave de la acción dicha, o null si el texto no es una.</summary>
    public string? Interpretar(string texto)
    {
        var normalizado = Normalizar(texto);
        if (normalizado.Length == 0) return null;

        // De la forma mas larga a la mas corta: si un dicho es prefijo de
        // otro, ganar con el corto se llevaria los dos en silencio.
        var candidatas = acciones.Todas
            .SelectMany(a => a.Dichos.Select(d => (a.Clave, Dicho: Normalizar(d))))
            .Where(c => c.Dicho.Length > 0)
            .OrderByDescending(c => c.Dicho.Length);

        foreach (var (clave, dicho) in candidatas)
            if (normalizado == dicho) return clave;

        return null;
    }

    /// <summary>Minúsculas, sin tildes, sin puntuación y con un solo espacio.</summary>
    private static string Normalizar(string texto)
    {
        var sinTildes = new string((texto ?? "")
            .Normalize(NormalizationForm.FormD)
            .Where(c => CharUnicodeInfo.GetUnicodeCategory(c) != UnicodeCategory.NonSpacingMark)
            .ToArray());

        return string.Join(' ', sinTildes.ToLowerInvariant()
            .Split([' ', ',', '.', ';', ':', '\t', '\n'], StringSplitOptions.RemoveEmptyEntries));
    }
}
```

- [ ] **Step 4: Correr y verificar que pasa**

Run: `dotnet test PokerProOS.slnx -p:SaltearFrontend=true --filter "FullyQualifiedName~InterpretadorDeRespuestaTests"`
Expected: PASS, 6 pruebas.

- [ ] **Step 5: El endpoint**

En `src/PokerProOS.Api/Controllers/EntrenadorController.cs`, agregar el record
arriba, junto a `TandaPedida`:

```csharp
/// <summary>Lo que se dijo, sin interpretar, más qué casilla se estaba contestando.</summary>
public record RespuestaHablada(
    string Situacion, string ClaveDeStack, string Spot, string Mano, string? Texto);
```

Agregar `InterpretadorDeRespuesta interprete` al constructor primario, después
de `IRegistroDeAcciones acciones`, y el método al final de la clase:

```csharp
    /// <summary>
    /// Contestar hablando. El texto que no es una acción se ignora con 200 y
    /// no cuenta como fallo: hablar cerca del micrófono no puede ensuciarte el
    /// calendario, y un 400 pintaría la consola de rojo por conversar.
    /// </summary>
    [HttpPost("respuesta-hablada")]
    public async Task<IActionResult> ResponderHablado(
        [FromBody] RespuestaHablada hablada, CancellationToken ct)
    {
        var accion = interprete.Interpretar(hablada.Texto ?? "");
        if (accion is null) return Ok(new { ignorado = true });

        var veredicto = await responder.ResponderAsync(
            UsuarioActual,
            new RespuestaEnviada(
                hablada.Situacion, hablada.ClaveDeStack, hablada.Spot, hablada.Mano, accion),
            Hoy, ct);

        return veredicto is null
            ? NotFound(new { error = "Esa casilla ya no existe en el catálogo." })
            : Ok(veredicto);
    }
```

- [ ] **Step 6: Registrarlo**

En `src/PokerProOS.Api/Program.cs`, junto a los `AddSingleton` de Application:

```csharp
builder.Services.AddSingleton<InterpretadorDeRespuesta>();
```

- [ ] **Step 7: El cliente**

En `frontend/src/core/services/entrenadorApi.ts`, agregar al final:

```typescript
/**
 * Contestar hablando. El servidor devuelve `{ ignorado: true }` cuando el
 * texto no era una acción: hablar cerca del micrófono no puede contar como
 * fallo, así que eso llega como null y la pregunta sigue abierta.
 */
export async function responderHablado(
  situacion: string, claveDeStack: string, spot: string, mano: string, texto: string,
): Promise<VeredictoDeRespuesta | null> {
  const v = await pedir<VeredictoDeRespuesta | { ignorado: true }>(
    '/api/entrenador/respuesta-hablada', 'POST',
    { situacion, claveDeStack, spot, mano, texto })

  return 'ignorado' in v ? null : v
}
```

- [ ] **Step 8: Cantar la pregunta**

El spec pide que el navegador **cante la mano y el spot**, no solo que escuche.
Sin eso, entrenar sigue atado a mirar la pantalla, que es justo lo que la voz
viene a resolver.

Crear `frontend/src/features/entrenador/useCantarPregunta.ts`:

```typescript
import { useEffect } from 'react'
import type { PreguntaDeTanda } from '../../core/models/catalogo.model'

/** Cómo se lee cada palo en voz alta. */
const PALOS: Record<string, string> = { s: 'suited', o: 'offsuit' }

/**
 * Dice la pregunta en voz alta: primero dónde estás, después la mano.
 *
 * La mano se deletrea —"A K offsuit" y no "AKo"— porque la síntesis lee la
 * etiqueta pegada como una palabra inventada. Es la misma razón por la que
 * RedactorDeRespuesta la deletrea del lado del servidor.
 *
 * Cancela lo que se esté diciendo antes de arrancar: speak() encola en vez de
 * reemplazar, así que sin esto pasar rápido de pregunta las apila y terminás
 * escuchando la de hace tres.
 */
export function useCantarPregunta(pregunta: PreguntaDeTanda | null, activo: boolean) {
  useEffect(() => {
    if (!pregunta || !activo || !('speechSynthesis' in window)) return

    const palo = pregunta.mano.length > 2 ? PALOS[pregunta.mano[2]] ?? '' : ''
    const mano = `${pregunta.mano[0]} ${pregunta.mano[1]} ${palo}`.trim()
    const frase = new SpeechSynthesisUtterance(
      `${pregunta.etiquetaDeSpot}, ${pregunta.claveDeStack}. ${mano}.`)
    frase.lang = 'es-ES'

    window.speechSynthesis.cancel()
    window.speechSynthesis.speak(frase)

    return () => window.speechSynthesis.cancel()
  }, [pregunta, activo])
}
```

- [ ] **Step 9: Conectar la voz en la pantalla**

En `frontend/src/features/entrenador/PaginaDeEntrenador.tsx`, importar
`responderHablado` junto a los demás y agregar, después de `elegir`:

```tsx
  /**
   * El mismo camino que el teclado, pero desde lo que se oyó. Si el texto no
   * era una acción, `responderHablado` devuelve null y la pregunta sigue
   * abierta: conversar al lado del micrófono no cuenta como fallo.
   */
  const contestarHablando = async (texto: string) => {
    if (!pregunta || veredicto) return
    try {
      const v = await responderHablado(
        pregunta.situacion, pregunta.claveDeStack, pregunta.spot, pregunta.mano, texto)
      if (!v) return
      setVeredicto(v)
      if (v.acerto) setAciertos((previo) => previo + 1)
    } catch (e) {
      setError(e instanceof Error ? e.message : 'No se pudo guardar la respuesta.')
    }
  }
```

Para que llegue texto hay que escuchar. `useVozDelNavegador` hoy manda todo a
`/api/voz/dictado`; para no tocarlo, la pantalla abre su propio motor de una
sola vez por pregunta, con el mismo patrón que `capturarDictado` ya usa en
`tablasApi.ts`.

Agregar el estado del interruptor junto a los demás `useState`:

```tsx
  // La voz se enciende a mano. Entrenando en silencio —de noche, o al lado de
  // alguien— cantar cada pregunta es peor que no tenerla.
  const [conVoz, setConVoz] = useState(false)
```

Y el botón, en la cabecera, antes del marcador:

```tsx
        <button
          type="button"
          className={conVoz ? 'boton-principal' : 'boton-tenue'}
          onClick={() => setConVoz((previo) => !previo)}
        >
          {conVoz ? 'Voz encendida' : 'Voz apagada'}
        </button>
```

Cantar, con el hook del paso anterior (importarlo desde `'./useCantarPregunta'`):

```tsx
  // Mientras hay veredicto no se canta: se está leyendo la explicación.
  useCantarPregunta(veredicto ? null : pregunta, conVoz)
```

Y escuchar. El efecto va **después** de la definición de `contestarHablando`,
para que se lea en el mismo orden en que ocurre:

```tsx
  // Escucha una respuesta por pregunta. Se reinicia con cada `pregunta`, y se
  // corta apenas hay veredicto para no oír la siguiente antes de tiempo.
  useEffect(() => {
    if (!pregunta || veredicto || !conVoz) return
    let cancelado = false
    void capturarDictado().then((r) => {
      if (!cancelado && r.texto) void contestarHablando(r.texto)
    })
    return () => { cancelado = true }
    // oxlint-disable-next-line exhaustive-deps
  }, [pregunta, veredicto, conVoz])
```

Importar `capturarDictado` desde `'../../core/services/tablasApi'`.

**Cuidado con el micrófono:** `capturarDictado` abre su propio motor sobre el
mismo micrófono que usa el copiloto de la pantalla de tablas. Si las dos están
escuchando a la vez, la Web Speech API devuelve `aborted`. Es aceptable —son
dos pantallas distintas y no se miran juntas— pero si aparece en la práctica, el
arreglo es que el entrenador use `capturar()` de `useVozDelNavegador`, que ya
pausa el motor continuo mientras graba.

- [ ] **Step 10: Verificar**

Run: `cd frontend && npx tsc -b && npx oxlint src`
Expected: las dos sin salida.

Run: `dotnet test PokerProOS.slnx -p:SaltearFrontend=true`
Expected: PASS, 285 pruebas.

- [ ] **Step 11: Probarlo de verdad**

```bash
dotnet build PokerProOS.slnx
dotnet run --project src/PokerProOS.Api
```

En **Spins › Entrenador**, arrancar una tanda con la voz encendida y comprobar
las dos mitades: que **cante** el spot, el stack y la mano al aparecer cada
pregunta, y que contestar diciendo "all in", "fold" o "pagar" dé el mismo
veredicto que el teclado. Hablar cualquier otra cosa no puede contar como fallo.

- [ ] **Step 12: Commit**

```bash
git add src frontend/src tests
git commit -m "feat: contestar el entrenador hablando, con los dichos de acciones.json"
```

---

## Al terminar

- [ ] `dotnet test PokerProOS.slnx -p:SaltearFrontend=true` — 285 verdes
- [ ] Agregar a `CLAUDE.md` una sección `### El entrenador` con: la unidad de
      repetición (la casilla, no la mano), la escalera de intervalos, que el
      material nuevo prioriza bordes, que el usuario se resuelve en
      `EntrenadorController.UsuarioActual`, y que es lo único de la app que
      necesita base de datos.
- [ ] Usar `superpowers:finishing-a-development-branch`.
