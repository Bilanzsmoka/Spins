# Ficha de memoria — Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Reemplazar el «en el borde, N manos» hablado por un popup escrito que explica la mano —ancla, umbral de stack, familias, peso en combos, la línea de spots y un tip editable— para poder memorizar las tablas en vez de sólo consultarlas.

**Architecture:** Un servicio nuevo de Application, `AnalizadorDeMemoria`, deduce cinco piezas leyendo el catálogo en memoria (`ICatalogoDeTablas`) y las devuelve en un `FichaDeMemoria`. La sexta pieza, el tip, se lee del JSON y se escribe con `IEditorDeTablas`, que ya existe. La ficha viaja por el evento SSE del copiloto y por un `GET` nuevo; el frontend la muestra en un popup que también absorbe el editor de celda actual.

**Tech Stack:** .NET 10 (C# 13, records posicionales, `System.Text.Json` / `JsonNode`), xUnit, React 19 + TypeScript + Vite, CSS plano en `frontend/src/index.css`, oxlint.

**Spec:** [docs/superpowers/specs/2026-08-26-ficha-de-memoria-design.md](../specs/2026-08-26-ficha-de-memoria-design.md)

## Global Constraints

- **Constantes desnudas:** el proyecto sólo permite los 13 rangos y el `169`. Los combos NO se hardcodean: se agrega `PalosPorRango = 4` y todo lo demás se deriva.
- **Nombres en castellano**, backend y frontend, como el resto del proyecto. Notación de póker (`Axs`, `Axo`, `AKo`) donde corresponde.
- **Dirección de dependencias:** `Domain ← Application ← Infrastructure ← Api`. `AnalizadorDeMemoria` va en Application y sólo depende de `ICatalogoDeTablas` y `Domain`.
- **Sin MediatR:** clases planas registradas a mano en `Program.cs`.
- **El JSON es la fuente de verdad.** Toda escritura pasa por `IEditorDeTablas`, que escribe a un temporal, mueve, y recarga el catálogo en caliente.
- **Comandos:** `dotnet test PokerProOS.slnx` corre los tests (los de voz manejan audio real y son lentos; filtrar con `--filter`). `dotnet build -p:SaltearFrontend=true` compila sin pagar el build de Vite. `npm run lint` en `frontend/` usa **oxlint**, no eslint.
- **Archivos de test:** `tests/PokerProOS.Tests/<Area>/<Nombre>Tests.cs`, xUnit, con `Rutas.Registro(...)` y `Rutas.SemillasDeTablas` para llegar a `database/`.
- **Datos reales usados en las aserciones** (verificados contra `database/seed-data/` el 2026-08-26, situación `HU_SB_OR_FISH`, spot `SB_OR`):
  - `A8o`: `ALL-IN` en 1-4bb…16bb, `CALL` en 17-18bb, `RAISE_X2` en 19-99bb.
  - A 17-18bb: `Axo` sube `AKo`→`A9o` y paga `A8o`→`A2o`; `Axs` sube `AKs`→`A7s` y paga `A6s`→`A2s`; los pares suben `AA`→`55` y `44`, `33`, `22` son `ALL-IN`.
  - A 17-18bb `SB_OR`: `CALL` 82 casillas, `RAISE_X2` 84, `ALL-IN` 3. En combos: `RAISE_X2` = 660 = 49,8 % de la baraja.
  - Orden de spots en `HU_SB_OR_FISH`: `SB_OR`, `VS_BB_ALL_IN`, `VS_BB_3BET`, `VS_BB_ISO_3BB`, `VS_BB_ISO_ALL_IN`. La etiqueta de `SB_OR` es `Mi acción · SB OR`.
  - `HU_BB_VS_MR_FISH` stack `1-5bb` tiene un solo spot: `BB_VS_SB_MR`.

---

### Task 1: Combos de baraja en Domain

**Files:**
- Modify: `src/PokerProOS.Domain/Manos/MatrizDeManos.cs`
- Test: `tests/PokerProOS.Tests/Manos/MatrizDeManosTests.cs`

**Interfaces:**
- Consumes: nada.
- Produces: `MatrizDeManos.PalosPorRango` (`const int`), `MatrizDeManos.Combos(string etiqueta)` → `int`, `MatrizDeManos.CombosTotales` (`int`, = 1326).

- [ ] **Step 1: Write the failing test**

Agregar al final de la clase en `tests/PokerProOS.Tests/Manos/MatrizDeManosTests.cs`:

```csharp
    [Theory]
    [InlineData("AA", 6)]
    [InlineData("22", 6)]
    [InlineData("AKs", 4)]
    [InlineData("72s", 4)]
    [InlineData("AKo", 12)]
    [InlineData("72o", 12)]
    public void Cuenta_los_combos_de_cada_forma_de_mano(string mano, int esperados)
        => Assert.Equal(esperados, MatrizDeManos.Combos(mano));

    [Fact]
    public void Los_combos_de_las_169_manos_son_la_baraja_entera()
    {
        var suma = MatrizDeManos.Todas().Sum(MatrizDeManos.Combos);
        Assert.Equal(1326, MatrizDeManos.CombosTotales);
        Assert.Equal(MatrizDeManos.CombosTotales, suma);
    }

    [Fact]
    public void Una_mano_desconocida_no_tiene_combos()
        => Assert.Throws<ArgumentException>(() => MatrizDeManos.Combos("XX"));
```

- [ ] **Step 2: Run test to verify it fails**

Run: `dotnet test PokerProOS.slnx --filter "FullyQualifiedName~MatrizDeManosTests"`
Expected: FAIL — no compila: `MatrizDeManos` no tiene `Combos` ni `CombosTotales`.

- [ ] **Step 3: Write minimal implementation**

En `src/PokerProOS.Domain/Manos/MatrizDeManos.cs`, agregar dentro de la clase, **después** de la línea `private static readonly IReadOnlyList<string> _todas = Construir();`:

```csharp
    /// <summary>
    /// La otra constante que el póker no cambia. De acá se derivan los combos:
    /// no se escriben 4, 6, 12 ni 1326 en ninguna parte del proyecto.
    /// </summary>
    public const int PalosPorRango = 4;

    /// <summary>C(52,2): todas las manos iniciales posibles de la baraja.</summary>
    public static int CombosTotales { get; } = _todas.Sum(Combos);

    /// <summary>
    /// Cuántas manos reales de la baraja representa una casilla de la grilla.
    /// Una pareja son las combinaciones de dos palos entre los cuatro, C(4,2);
    /// una suited es una por palo; una offsuit es cada palo del rango alto
    /// contra cada palo distinto del bajo.
    /// </summary>
    public static int Combos(string etiqueta)
    {
        var (fila, columna) = Coordenadas(etiqueta);
        if (fila == columna) return PalosPorRango * (PalosPorRango - 1) / 2;
        return etiqueta[2] == 's' ? PalosPorRango : PalosPorRango * (PalosPorRango - 1);
    }
```

**Ojo con el orden de inicialización:** `CombosTotales` lee `_todas`, así que su declaración tiene que quedar después de la de `_todas`. Si el test de la suma da `0`, es exactamente esto.

`Coordenadas` ya lanza `ArgumentException` para una mano desconocida, así que el tercer test pasa sin código extra.

- [ ] **Step 4: Run tests to verify they pass**

Run: `dotnet test PokerProOS.slnx --filter "FullyQualifiedName~MatrizDeManosTests"`
Expected: PASS, todos.

- [ ] **Step 5: Commit**

```bash
git add src/PokerProOS.Domain/Manos/MatrizDeManos.cs tests/PokerProOS.Tests/Manos/MatrizDeManosTests.cs
git commit -m "feat: combos de baraja por casilla de la grilla"
```

---

### Task 2: El tipo `FichaDeMemoria` y el peso de baraja

**Files:**
- Create: `src/PokerProOS.Application/Tablas/FichaDeMemoria.cs`
- Create: `src/PokerProOS.Application/Tablas/AnalizadorDeMemoria.cs`
- Test: `tests/PokerProOS.Tests/Tablas/AnalizadorDeMemoriaTests.cs`

**Interfaces:**
- Consumes: `MatrizDeManos.Combos`, `MatrizDeManos.CombosTotales` (Task 1); `ICatalogoDeTablas`, `SpotDeTabla`, `CeldaDeTabla`, `ParteDeMix` (ya existen).
- Produces:
  - `record PesoDeAccion(string Accion, double Combos, double PorcentajeDeBaraja)`
  - `record AnclaDeFamilia(string Familia, string Tope, string Fondo, string Accion, string? Siguiente, string? AccionSiguiente)`
  - `record BandaDeStack(string ClaveDeStack, decimal MinBB, decimal MaxBB, string Accion, bool EsElActual)`
  - `record PasoDeLinea(string Spot, string Etiqueta, string Accion, bool EsElConsultado)`
  - `record FichaDeMemoria(string Mano, string Accion, string ClaveDeStack, IReadOnlyList<PesoDeAccion> Pesos, AnclaDeFamilia? Ancla, IReadOnlyList<BandaDeStack> Umbral, IReadOnlyList<AnclaDeFamilia> Familias, IReadOnlyList<PasoDeLinea> Linea, string? Tip)`
  - `sealed class AnalizadorDeMemoria(ICatalogoDeTablas catalogo)` con `FichaDeMemoria? Analizar(string situacion, string claveDeStack, string claveDeSpot, string mano)`

- [ ] **Step 1: Write the failing test**

Crear `tests/PokerProOS.Tests/Tablas/AnalizadorDeMemoriaTests.cs`:

```csharp
using PokerProOS.Application.Tablas;
using PokerProOS.Infrastructure.Tablas;

namespace PokerProOS.Tests.Tablas;

public class AnalizadorDeMemoriaTests
{
    private static AnalizadorDeMemoria Analizador()
    {
        var acciones = RegistroDeAccionesJson.Cargar(Rutas.Registro("acciones.json"));
        return new(new CargadorDeTablas(new ValidadorDeTabla(acciones), acciones)
            .CargarDirectorio(Rutas.SemillasDeTablas));
    }

    private static FichaDeMemoria Ficha(
        string mano, string stack = "17-18bb", string spot = "SB_OR",
        string situacion = "HU_SB_OR_FISH")
        => Analizador().Analizar(situacion, stack, spot, mano)!;

    [Fact]
    public void Sin_ficha_cuando_el_spot_no_existe()
        => Assert.Null(Analizador().Analizar("HU_SB_OR_FISH", "17-18bb", "NO_EXISTE", "A8o"));

    [Fact]
    public void Sin_ficha_cuando_la_mano_no_existe()
        => Assert.Null(Analizador().Analizar("HU_SB_OR_FISH", "17-18bb", "SB_OR", "XX"));

    [Fact]
    public void Trae_la_accion_de_la_mano()
    {
        var ficha = Ficha("A8o");
        Assert.Equal("A8o", ficha.Mano);
        Assert.Equal("CALL", ficha.Accion);
        Assert.Equal("17-18bb", ficha.ClaveDeStack);
    }

    [Fact]
    public void El_peso_se_mide_en_combos_de_baraja_no_en_casillas()
    {
        var ficha = Ficha("A8o");
        var raise = ficha.Pesos.Single(p => p.Accion == "RAISE_X2");

        // 84 casillas de 169 son 49,7 %, pero lo que importa es la baraja.
        // 660.0, no 660: la sobrecarga con precisión de xUnit es de double.
        Assert.Equal(660.0, raise.Combos, 3);
        Assert.Equal(49.8, raise.PorcentajeDeBaraja, 1);
    }

    [Fact]
    public void Los_pesos_del_spot_suman_la_baraja_entera()
    {
        var ficha = Ficha("A8o");
        Assert.Equal(100.0, ficha.Pesos.Sum(p => p.PorcentajeDeBaraja), 6);
    }

    [Fact]
    public void Los_pesos_vienen_ordenados_de_mayor_a_menor()
    {
        var pesos = Ficha("A8o").Pesos.Select(p => p.Combos).ToList();
        Assert.Equal(pesos.OrderByDescending(c => c).ToList(), pesos);
    }
}
```

- [ ] **Step 2: Run test to verify it fails**

Run: `dotnet test PokerProOS.slnx --filter "FullyQualifiedName~AnalizadorDeMemoriaTests"`
Expected: FAIL — no compila: no existe `AnalizadorDeMemoria`.

- [ ] **Step 3: Write minimal implementation**

Crear `src/PokerProOS.Application/Tablas/FichaDeMemoria.cs`:

```csharp
namespace PokerProOS.Application.Tablas;

/// <summary>
/// Cuánta baraja se lleva una acción. En combos, no en casillas: una casilla
/// suited son cuatro manos reales y una offsuit son doce, así que contar
/// casillas exagera lo suited justo donde uno quiere calcular.
/// </summary>
/// <param name="Combos">
/// Fraccionario porque una celda mixta reparte sus combos entre sus acciones
/// según la frecuencia declarada.
/// </param>
public record PesoDeAccion(string Accion, double Combos, double PorcentajeDeBaraja);

/// <summary>
/// El bloque contiguo de una familia que comparte una acción, y la mano que lo
/// rompe. Es la forma de acordarse de un rango sin memorizar mano por mano:
/// alcanza con el fondo del bloque.
/// </summary>
/// <param name="Familia">Notación de póker: "Axs", "Axo", "Pares".</param>
/// <param name="Tope">La mano más alta del bloque.</param>
/// <param name="Fondo">La más baja: la mano ancla.</param>
/// <param name="Siguiente">
/// La primera que ya no entra, o nulo si el bloque llega al final de la familia.
/// </param>
public record AnclaDeFamilia(
    string Familia,
    string Tope,
    string Fondo,
    string Accion,
    string? Siguiente,
    string? AccionSiguiente);

/// <summary>Un tramo de stacks donde la mano hace siempre lo mismo.</summary>
/// <param name="ClaveDeStack">
/// La clave del stack, o "{primero}…{ultimo}" si la banda junta varios.
/// </param>
/// <param name="EsElActual">
/// Si el stack consultado cae adentro de esta banda. Se marca acá y no se
/// deduce comparando claves en la pantalla: una banda que junta varios
/// stacks no lleva la clave de ninguno de ellos, así que la comparación
/// fallaría justo cuando el stack que estás jugando quedó fusionado.
/// </param>
public record BandaDeStack(
    string ClaveDeStack, decimal MinBB, decimal MaxBB, string Accion, bool EsElActual);

/// <summary>Un spot del stack y lo que esa mano hace ahí.</summary>
public record PasoDeLinea(string Spot, string Etiqueta, string Accion, bool EsElConsultado);

/// <summary>
/// Todo lo que se puede decir de una mano en un spot sin inventar nada: cinco
/// piezas deducidas del catálogo y el tip escrito a mano, si lo hay.
/// </summary>
public record FichaDeMemoria(
    string Mano,
    string Accion,
    string ClaveDeStack,
    IReadOnlyList<PesoDeAccion> Pesos,
    AnclaDeFamilia? Ancla,
    IReadOnlyList<BandaDeStack> Umbral,
    IReadOnlyList<AnclaDeFamilia> Familias,
    IReadOnlyList<PasoDeLinea> Linea,
    string? Tip);
```

Crear `src/PokerProOS.Application/Tablas/AnalizadorDeMemoria.cs`:

```csharp
using PokerProOS.Domain.Manos;

namespace PokerProOS.Application.Tablas;

/// <summary>
/// Explica una mano en vez de sólo responderla. Deduce todo del catálogo en
/// memoria: no guarda nada propio, así que una tabla corregida cambia la
/// explicación en el acto.
/// </summary>
public sealed class AnalizadorDeMemoria(ICatalogoDeTablas catalogo)
{
    public FichaDeMemoria? Analizar(
        string situacion, string claveDeStack, string claveDeSpot, string mano)
    {
        var spot = catalogo.Spot(situacion, claveDeStack, claveDeSpot);
        var celda = spot?.CeldaDe(mano);
        if (spot is null || celda is null) return null;

        return new FichaDeMemoria(
            celda.Mano,
            celda.Accion,
            claveDeStack,
            Pesos(spot),
            null,
            [],
            [],
            [],
            null);
    }

    /// <summary>
    /// Una celda mixta reparte sus combos entre sus acciones según la
    /// frecuencia: contarla entera en las dos haría que los porcentajes
    /// sumaran más de 100 y el número dejaría de significar "de la baraja".
    /// </summary>
    private static IReadOnlyList<PesoDeAccion> Pesos(SpotDeTabla spot)
    {
        var combos = new Dictionary<string, double>(StringComparer.OrdinalIgnoreCase);

        foreach (var celda in spot.Celdas)
        {
            var deLaCelda = MatrizDeManos.Combos(celda.Mano);
            if (celda.Mix is { Count: > 1 } partes)
                foreach (var parte in partes)
                    Sumar(parte.Accion, deLaCelda * parte.Frecuencia / 100.0);
            else
                Sumar(celda.Accion, deLaCelda);
        }

        return combos
            .OrderByDescending(par => par.Value)
            .Select(par => new PesoDeAccion(
                par.Key, par.Value, par.Value * 100.0 / MatrizDeManos.CombosTotales))
            .ToList();

        void Sumar(string accion, double cuantos)
            => combos[accion] = combos.GetValueOrDefault(accion) + cuantos;
    }
}
```

- [ ] **Step 4: Run tests to verify they pass**

Run: `dotnet test PokerProOS.slnx --filter "FullyQualifiedName~AnalizadorDeMemoriaTests"`
Expected: PASS, los seis.

- [ ] **Step 5: Commit**

```bash
git add src/PokerProOS.Application/Tablas/FichaDeMemoria.cs src/PokerProOS.Application/Tablas/AnalizadorDeMemoria.cs tests/PokerProOS.Tests/Tablas/AnalizadorDeMemoriaTests.cs
git commit -m "feat: ficha de memoria con el peso de baraja de cada accion"
```

---

### Task 3: La mano ancla dentro de su familia

**Files:**
- Modify: `src/PokerProOS.Application/Tablas/AnalizadorDeMemoria.cs`
- Test: `tests/PokerProOS.Tests/Tablas/AnalizadorDeMemoriaTests.cs`

**Interfaces:**
- Consumes: `FichaDeMemoria`, `AnclaDeFamilia`, `AnalizadorDeMemoria.Analizar` (Task 2).
- Produces: `FichaDeMemoria.Ancla` deja de ser nulo. Métodos privados nuevos: `Familia(string mano)` → `(string Nombre, List<string> Manos)`, `Ancla(SpotDeTabla spot, string mano)` → `AnclaDeFamilia?`, `Igual(string?, string?)` → `bool`.

**Definición.** La familia de una mano es la lista ordenada de arriba hacia abajo:
- pareja → `Pares`: `AA, KK, QQ, JJ, TT, 99, 88, 77, 66, 55, 44, 33, 22`.
- suited `Xys` → `Xxs`: `X` contra cada rango más bajo, de mayor a menor (`AKs, AQs, … A2s`).
- offsuit `Xyo` → `Xxo`: igual, con `o`.

El ancla es el bloque **contiguo** de esa lista que contiene la mano y comparte su acción. `Siguiente` es la primera mano después del `Fondo`, o nulo si el bloque termina la familia. Si el bloque abarca la familia entera no hay ancla: nada se corta, no hay nada que recordar.

- [ ] **Step 1: Write the failing test**

Agregar a `AnalizadorDeMemoriaTests`:

```csharp
    [Fact]
    public void El_ancla_dice_donde_se_corta_la_familia()
    {
        var ancla = Ficha("A8o").Ancla!;
        Assert.Equal("Axo", ancla.Familia);
        Assert.Equal("A8o", ancla.Tope);
        Assert.Equal("A2o", ancla.Fondo);
        Assert.Equal("CALL", ancla.Accion);
        // El bloque de CALL llega hasta el final de la familia.
        Assert.Null(ancla.Siguiente);
    }

    [Fact]
    public void El_ancla_de_la_mano_de_arriba_apunta_a_la_que_rompe()
    {
        var ancla = Ficha("AKo").Ancla!;
        Assert.Equal("Axo", ancla.Familia);
        Assert.Equal("AKo", ancla.Tope);
        Assert.Equal("A9o", ancla.Fondo);
        Assert.Equal("RAISE_X2", ancla.Accion);
        Assert.Equal("A8o", ancla.Siguiente);
        Assert.Equal("CALL", ancla.AccionSiguiente);
    }

    [Fact]
    public void El_ancla_de_una_pareja_se_mide_contra_los_pares()
    {
        var ancla = Ficha("77").Ancla!;
        Assert.Equal("Pares", ancla.Familia);
        Assert.Equal("AA", ancla.Tope);
        Assert.Equal("55", ancla.Fondo);
        Assert.Equal("44", ancla.Siguiente);
        Assert.Equal("ALL-IN", ancla.AccionSiguiente);
    }

    [Fact]
    public void Una_familia_entera_de_la_misma_accion_no_tiene_ancla()
    {
        // A 8bb el spot no tiene folds y todo Axo es ALL-IN.
        Assert.Null(Ficha("A8o", stack: "8bb").Ancla);
    }
```

- [ ] **Step 2: Run test to verify it fails**

Run: `dotnet test PokerProOS.slnx --filter "FullyQualifiedName~AnalizadorDeMemoriaTests"`
Expected: FAIL — los tres primeros con `NullReferenceException` (`Ancla` es nulo). El cuarto pasa, pero por la razón equivocada: eso lo cubren los otros tres.

- [ ] **Step 3: Write minimal implementation**

En `AnalizadorDeMemoria`, cambiar el quinto argumento de `new FichaDeMemoria(...)` de `null` a `Ancla(spot, celda.Mano)`, y agregar:

```csharp
    /// <summary>
    /// La familia de una mano, ordenada de mayor a menor: los pares, o el
    /// rango alto contra cada kicker. Es el eje por el que se recuerda un
    /// rango — "hasta A9o" dice más que trece manos sueltas.
    /// </summary>
    private static (string Nombre, List<string> Manos) Familia(string mano)
    {
        if (mano.Length == 2)
            return ("Pares", MatrizDeManos.Rangos.Select(r => $"{r}{r}").ToList());

        var alto = mano[0];
        var palo = mano[2];
        var manos = MatrizDeManos.Rangos
            .Skip(MatrizDeManos.IndiceDeRango(alto) + 1)
            .Select(bajo => $"{alto}{bajo}{palo}")
            .ToList();
        return ($"{alto}x{palo}", manos);
    }

    /// <summary>
    /// El bloque contiguo de la familia que contiene a la mano y comparte su
    /// acción. Si la familia entera hace lo mismo no hay nada que anclar: el
    /// ancla existe para marcar dónde se corta.
    /// </summary>
    private static AnclaDeFamilia? Ancla(SpotDeTabla spot, string mano)
    {
        var (nombre, familia) = Familia(mano);
        var accion = spot.AccionDe(mano);
        if (accion is null) return null;

        var indice = familia.IndexOf(mano);
        if (indice < 0) return null;

        var desde = indice;
        while (desde > 0 && Igual(spot.AccionDe(familia[desde - 1]), accion)) desde--;

        var hasta = indice;
        while (hasta < familia.Count - 1 && Igual(spot.AccionDe(familia[hasta + 1]), accion)) hasta++;

        if (desde == 0 && hasta == familia.Count - 1) return null;

        var siguiente = hasta < familia.Count - 1 ? familia[hasta + 1] : null;
        return new AnclaDeFamilia(
            nombre,
            familia[desde],
            familia[hasta],
            accion,
            siguiente,
            siguiente is null ? null : spot.AccionDe(siguiente));
    }

    private static bool Igual(string? a, string? b)
        => string.Equals(a, b, StringComparison.OrdinalIgnoreCase);
```

`Familia` devuelve `List<string>` a propósito: `IReadOnlyList<string>` no expone `IndexOf`, que es de `IList<T>` — el mismo detalle que ya está comentado en `MatrizDeManos.IndiceDeRango`.

- [ ] **Step 4: Run tests to verify they pass**

Run: `dotnet test PokerProOS.slnx --filter "FullyQualifiedName~AnalizadorDeMemoriaTests"`
Expected: PASS, los diez.

- [ ] **Step 5: Commit**

```bash
git add src/PokerProOS.Application/Tablas/AnalizadorDeMemoria.cs tests/PokerProOS.Tests/Tablas/AnalizadorDeMemoriaTests.cs
git commit -m "feat: mano ancla dentro de su familia"
```

---

### Task 4: El umbral de stack

**Files:**
- Modify: `src/PokerProOS.Application/Tablas/AnalizadorDeMemoria.cs`
- Test: `tests/PokerProOS.Tests/Tablas/AnalizadorDeMemoriaTests.cs`

**Interfaces:**
- Consumes: `BandaDeStack` (Task 2), `Igual` (Task 3), `ICatalogoDeTablas.Situacion`, `SituacionDeTabla.Stacks`, `TablaDeStack.Stack` (`RangoDeStack` con `Clave`, `MinBB`, `MaxBB`), `TablaDeStack.Spot`.
- Produces: `FichaDeMemoria.Umbral` poblado. Métodos privados `Umbral(string situacion, string claveDeStack, string claveDeSpot, string mano)` y `Unir(string acumulado, string ultimo)`.

**Definición.** Recorrer los stacks de la situación (ya vienen ordenados por `MinBB` desde `CargadorDeTablas`), tomar la acción de esa mano en ese spot, y colapsar tramos consecutivos de igual acción en una sola banda. Un stack que no declara el spot corta el tramo. La `ClaveDeStack` de una banda que abarca varios stacks es `"{primera}…{última}"` — claves, no números, así que la última conserva su nombre entero (`"1-4bb…13-16bb"`, no `"1-4bb…16bb"`); `MinBB`/`MaxBB` son los extremos reales. `EsElActual` se marca en la banda que contiene el stack consultado, sea sola o fusionada.

- [ ] **Step 1: Write the failing test**

```csharp
    [Fact]
    public void El_umbral_colapsa_los_stacks_que_hacen_lo_mismo()
    {
        var umbral = Ficha("A8o").Umbral;

        Assert.Equal(3, umbral.Count);

        Assert.Equal("ALL-IN", umbral[0].Accion);
        Assert.Equal(1m, umbral[0].MinBB);
        Assert.Equal(16m, umbral[0].MaxBB);

        Assert.Equal("CALL", umbral[1].Accion);
        Assert.Equal(17m, umbral[1].MinBB);
        Assert.Equal(18m, umbral[1].MaxBB);
        Assert.Equal("17-18bb", umbral[1].ClaveDeStack);
        Assert.True(umbral[1].EsElActual);
        Assert.False(umbral[0].EsElActual);
        Assert.False(umbral[2].EsElActual);

        Assert.Equal("RAISE_X2", umbral[2].Accion);
        Assert.Equal(19m, umbral[2].MinBB);
        Assert.Equal(99m, umbral[2].MaxBB);
    }

    [Fact]
    public void Una_banda_de_varios_stacks_nombra_sus_extremos()
    {
        // Extremos por CLAVE, no por número: el último tramo entra con su
        // nombre entero. Nueve stacks (1-4bb … 13-16bb) colapsan en uno.
        Assert.Equal("1-4bb…13-16bb", Ficha("A8o").Umbral[0].ClaveDeStack);
    }

    [Fact]
    public void La_banda_actual_se_marca_aunque_este_fusionada()
    {
        // A 10bb, A8o cae adentro de la banda ALL-IN que junta nueve stacks.
        // Comparar claves no serviría: la banda no se llama "10bb".
        var umbral = Ficha("A8o", stack: "10bb").Umbral;
        var actual = umbral.Single(b => b.EsElActual);
        Assert.Equal("ALL-IN", actual.Accion);
        Assert.Equal("1-4bb…13-16bb", actual.ClaveDeStack);
    }

    [Fact]
    public void El_umbral_de_una_mano_fuerte_igual_se_calcula()
    {
        var umbral = Ficha("AA").Umbral;
        Assert.NotEmpty(umbral);
        Assert.All(umbral, banda => Assert.False(string.IsNullOrEmpty(banda.Accion)));
    }
```

- [ ] **Step 2: Run test to verify it fails**

Run: `dotnet test PokerProOS.slnx --filter "FullyQualifiedName~AnalizadorDeMemoriaTests"`
Expected: FAIL — `umbral.Count` es 0, no 3.

- [ ] **Step 3: Write minimal implementation**

Cambiar el sexto argumento de `new FichaDeMemoria(...)` de `[]` a `Umbral(situacion, claveDeStack, claveDeSpot, celda.Mano)`, y agregar:

```csharp
    /// <summary>
    /// La misma mano a lo largo de todos los stacks de la situación,
    /// colapsada en tramos de igual acción. Es la forma en que se estudian
    /// estos rangos: no trece tablas sueltas, sino dos o tres cortes.
    /// </summary>
    private IReadOnlyList<BandaDeStack> Umbral(
        string situacion, string claveDeStack, string claveDeSpot, string mano)
    {
        var stacks = catalogo.Situacion(situacion)?.Stacks;
        if (stacks is null) return [];

        var bandas = new List<BandaDeStack>();
        foreach (var tabla in stacks)
        {
            var accion = tabla.Spot(claveDeSpot)?.AccionDe(mano);
            if (accion is null) continue;

            var esElActual = Igual(tabla.Stack.Clave, claveDeStack);
            var ultima = bandas.Count > 0 ? bandas[^1] : null;

            // Se extiende sólo si el stack anterior pega con éste: un stack
            // sin este spot corta el tramo, porque entre medio la tabla no
            // dice nada y fingir continuidad sería inventar.
            var continua = ultima is not null
                && Igual(ultima.Accion, accion)
                && ultima.MaxBB == tabla.Stack.MinBB - 1;

            if (continua)
                bandas[^1] = ultima! with
                {
                    ClaveDeStack = Unir(ultima.ClaveDeStack, tabla.Stack.Clave),
                    MaxBB = tabla.Stack.MaxBB,
                    // La banda es la actual si CUALQUIERA de los stacks que
                    // absorbió lo es, no sólo el primero.
                    EsElActual = ultima.EsElActual || esElActual,
                };
            else
                bandas.Add(new BandaDeStack(
                    tabla.Stack.Clave, tabla.Stack.MinBB, tabla.Stack.MaxBB, accion, esElActual));
        }
        return bandas;
    }

    /// <summary>
    /// El nombre de una banda que abarca varios stacks: sus extremos. Se
    /// recorta lo ya unido para que tres stacks no den "a…b…c".
    /// </summary>
    private static string Unir(string acumulado, string ultimo)
    {
        var primero = acumulado.Split('…')[0];
        return primero == ultimo ? ultimo : $"{primero}…{ultimo}";
    }
```

**Ojo:** `ultima.MaxBB == tabla.Stack.MinBB - 1` supone stacks en enteros consecutivos, que es como están declarados los trece archivos (`1-4`, `5`, `6`, … `19-99`). Es intencional: dos stacks con un hueco entre medio no forman una banda.

- [ ] **Step 4: Run tests to verify they pass**

Run: `dotnet test PokerProOS.slnx --filter "FullyQualifiedName~AnalizadorDeMemoriaTests"`
Expected: PASS, los catorce.

- [ ] **Step 5: Commit**

```bash
git add src/PokerProOS.Application/Tablas/AnalizadorDeMemoria.cs tests/PokerProOS.Tests/Tablas/AnalizadorDeMemoriaTests.cs
git commit -m "feat: umbral de stack de una mano"
```

---

### Task 5: Las familias emparentadas y la línea de spots

**Files:**
- Modify: `src/PokerProOS.Application/Tablas/AnalizadorDeMemoria.cs`
- Test: `tests/PokerProOS.Tests/Tablas/AnalizadorDeMemoriaTests.cs`

**Interfaces:**
- Consumes: `AnclaDeFamilia`, `PasoDeLinea` (Task 2), `Ancla` e `Igual` (Task 3), `ICatalogoDeTablas.StackPorClave`, `TablaDeStack.Spots`, `SpotDeTabla.Clave` / `.Etiqueta` / `.AccionDe`.
- Produces: `FichaDeMemoria.Familias` y `FichaDeMemoria.Linea` poblados. Métodos privados `Familias(SpotDeTabla spot, string mano)` y `Linea(string situacion, string claveDeStack, string claveDeSpot, string mano)`.

**`Familias`.** El ancla del bloque que **encabeza** cada familia emparentada con la mano: para `A8o` son `Axs` (pedida por `AKs`), `Axo` (por `AKo`) y `Pares` (por `AA`). Para una pareja, sólo `Pares`. Las que devuelven nulo (familia uniforme) se descartan.

**`Linea`.** Todos los spots del stack, en el orden en que el JSON los declara, con la acción de esa mano en cada uno y `EsElConsultado` marcando el pedido.

- [ ] **Step 1: Write the failing test**

```csharp
    [Fact]
    public void Las_familias_emparentadas_son_las_dos_del_rango_alto_y_los_pares()
    {
        var familias = Ficha("A8o").Familias;

        // new[] y ToArray(): una expresión de colección como argumento de
        // Assert.Equal no tiene tipo destino y no resuelve la sobrecarga.
        Assert.Equal(new[] { "Axs", "Axo", "Pares" }, familias.Select(f => f.Familia).ToArray());

        var suited = familias.Single(f => f.Familia == "Axs");
        Assert.Equal("AKs", suited.Tope);
        Assert.Equal("A7s", suited.Fondo);
        Assert.Equal("RAISE_X2", suited.Accion);
        Assert.Equal("A6s", suited.Siguiente);

        var offsuit = familias.Single(f => f.Familia == "Axo");
        Assert.Equal("A9o", offsuit.Fondo);

        var pares = familias.Single(f => f.Familia == "Pares");
        Assert.Equal("55", pares.Fondo);
        Assert.Equal("44", pares.Siguiente);
        Assert.Equal("ALL-IN", pares.AccionSiguiente);
    }

    [Fact]
    public void Una_pareja_solo_empareja_con_los_pares()
        => Assert.Equal(new[] { "Pares" }, Ficha("77").Familias.Select(f => f.Familia).ToArray());

    [Fact]
    public void La_linea_recorre_los_spots_del_stack_en_orden()
    {
        var linea = Ficha("A8o").Linea;

        Assert.Equal(
            new[] { "SB_OR", "VS_BB_ALL_IN", "VS_BB_3BET", "VS_BB_ISO_3BB", "VS_BB_ISO_ALL_IN" },
            linea.Select(p => p.Spot).ToArray());
        Assert.Equal("Mi acción · SB OR", linea[0].Etiqueta);
        Assert.True(linea[0].EsElConsultado);
        Assert.All(linea.Skip(1), paso => Assert.False(paso.EsElConsultado));
        Assert.All(linea, paso => Assert.False(string.IsNullOrEmpty(paso.Accion)));
    }

    [Fact]
    public void Un_stack_con_un_solo_spot_da_una_linea_de_un_paso()
    {
        var ficha = Ficha("A8o",
            situacion: "HU_BB_VS_MR_FISH", stack: "1-5bb", spot: "BB_VS_SB_MR");
        Assert.Single(ficha.Linea);
        Assert.True(ficha.Linea[0].EsElConsultado);
    }
```

- [ ] **Step 2: Run test to verify it fails**

Run: `dotnet test PokerProOS.slnx --filter "FullyQualifiedName~AnalizadorDeMemoriaTests"`
Expected: FAIL — `Familias` y `Linea` vienen vacías.

- [ ] **Step 3: Write minimal implementation**

Cambiar el séptimo y octavo argumento de `new FichaDeMemoria(...)` de `[]`, `[]` a `Familias(spot, celda.Mano)` y `Linea(situacion, claveDeStack, claveDeSpot, celda.Mano)`, y agregar:

```csharp
    /// <summary>
    /// Las familias que comparten sangre con la mano: las dos de su rango alto
    /// y los pares. Se reporta el bloque que encabeza cada una —"sube hasta
    /// acá"—, que es la forma en que se recuerdan estos rangos. Una familia
    /// uniforme no aporta corte y se descarta.
    /// </summary>
    private static IReadOnlyList<AnclaDeFamilia> Familias(SpotDeTabla spot, string mano)
    {
        var cabezas = new List<string>();
        if (mano.Length > 2)
        {
            var alto = mano[0];
            var siguiente = MatrizDeManos.Rangos[MatrizDeManos.IndiceDeRango(alto) + 1];
            cabezas.Add($"{alto}{siguiente}s");
            cabezas.Add($"{alto}{siguiente}o");
        }
        cabezas.Add($"{MatrizDeManos.Rangos[0]}{MatrizDeManos.Rangos[0]}");

        return cabezas
            .Select(cabeza => Ancla(spot, cabeza))
            .OfType<AnclaDeFamilia>()
            .ToList();
    }

    /// <summary>
    /// Qué hace esa misma mano en cada spot del stack, en el orden en que el
    /// JSON los declara — que ya es el orden en que pasan las cosas en la
    /// mano: primero la mía, después lo que el rival me haga.
    /// </summary>
    private IReadOnlyList<PasoDeLinea> Linea(
        string situacion, string claveDeStack, string claveDeSpot, string mano)
    {
        var tabla = catalogo.StackPorClave(situacion, claveDeStack);
        if (tabla is null) return [];

        return tabla.Spots
            .Select(s => new PasoDeLinea(
                s.Clave, s.Etiqueta, s.AccionDe(mano) ?? "", Igual(s.Clave, claveDeSpot)))
            .Where(paso => paso.Accion.Length > 0)
            .ToList();
    }
```

**Sobre `IndiceDeRango(alto) + 1`:** el rango alto de una mano no-pareja nunca es el último de los trece, porque siempre existe uno más bajo — así que el índice no se sale. Y una mano inválida ya devolvió nulo en `Analizar` antes de llegar acá.

- [ ] **Step 4: Run tests to verify they pass**

Run: `dotnet test PokerProOS.slnx --filter "FullyQualifiedName~AnalizadorDeMemoriaTests"`
Expected: PASS, los dieciocho.

- [ ] **Step 5: Commit**

```bash
git add src/PokerProOS.Application/Tablas/AnalizadorDeMemoria.cs tests/PokerProOS.Tests/Tablas/AnalizadorDeMemoriaTests.cs
git commit -m "feat: familias emparentadas y linea de spots del stack"
```

---

### Task 6: El tip en el JSON — carga y validación

**Files:**
- Modify: `src/PokerProOS.Application/Tablas/ICatalogoDeTablas.cs` (el record `SpotDeTabla`)
- Modify: `src/PokerProOS.Infrastructure/Tablas/CargadorDeTablas.cs` (método `LeerSpot`)
- Modify: `src/PokerProOS.Infrastructure/Tablas/ValidadorDeTabla.cs` (método `ValidarSpot`)
- Modify: `src/PokerProOS.Application/Tablas/AnalizadorDeMemoria.cs`
- Test: `tests/PokerProOS.Tests/Tablas/ValidadorDeTablaTests.cs`, `tests/PokerProOS.Tests/Tablas/AnalizadorDeMemoriaTests.cs`

**Interfaces:**
- Consumes: `FichaDeMemoria.Tip` (Task 2).
- Produces: `SpotDeTabla.Tip` (`string?`, parámetro posicional **opcional al final** del record, para no romper las construcciones existentes).

- [ ] **Step 1: Write the failing test**

En `AnalizadorDeMemoriaTests`:

```csharp
    [Fact]
    public void Sin_tip_declarado_la_ficha_no_trae_tip()
        => Assert.Null(Ficha("A8o").Tip);
```

En `ValidadorDeTablaTests.cs` — mirar primero cómo arman archivos temporales los tests que ya están ahí y seguir ese patrón. Si hay un helper que escribe JSON a un temporal y devuelve los problemas, reusarlo; si no, escribirlo con `Path.GetTempFileName()`, `File.WriteAllText`, `validador.Validar(ruta)` y `File.Delete` en un `try/finally`. Los dos tests nuevos:

```csharp
    [Fact]
    public void Un_tip_vacio_es_un_problema()
    {
        var problemas = ValidarJson("""
        {
          "situation": { "key": "X", "label": "X" },
          "stacks": [{ "key": "10bb", "minBB": 10, "maxBB": 10, "spots": [
            { "key": "S", "label": "S", "tip": "   ", "actions": { "FOLD": "REST" } }
          ]}]
        }
        """);
        Assert.Contains(problemas, p => p.Mensaje.Contains("tip"));
    }

    [Fact]
    public void Un_spot_sin_tip_no_es_un_problema()
    {
        var problemas = ValidarJson("""
        {
          "situation": { "key": "X", "label": "X" },
          "stacks": [{ "key": "10bb", "minBB": 10, "maxBB": 10, "spots": [
            { "key": "S", "label": "S", "actions": { "FOLD": "REST" } }
          ]}]
        }
        """);
        Assert.DoesNotContain(problemas, p => p.Mensaje.Contains("tip"));
    }
```

- [ ] **Step 2: Run tests to verify they fail**

Run: `dotnet test PokerProOS.slnx --filter "FullyQualifiedName~ValidadorDeTablaTests"`
Expected: FAIL — `Un_tip_vacio_es_un_problema` no encuentra ningún problema que mencione «tip».

- [ ] **Step 3: Write minimal implementation**

En `src/PokerProOS.Application/Tablas/ICatalogoDeTablas.cs`:

```csharp
public record SpotDeTabla(
    string Clave,
    string Etiqueta,
    IReadOnlyList<CeldaDeTabla> Celdas,
    /// <summary>
    /// El porqué escrito a mano: lo único de la ficha que ningún cálculo puede
    /// deducir de la tabla. Nulo si el spot no lo declara.
    /// </summary>
    string? Tip = null)
```

(el cuerpo del record —`_porMano`, `Conteos`, `AccionDe`, `CeldaDe`— queda igual.)

En `CargadorDeTablas.LeerSpot`, cambiar el `return` final:

```csharp
        return new SpotDeTabla(
            spot.GetProperty("key").GetString()!,
            spot.GetProperty("label").GetString()!,
            celdas,
            spot.TryGetProperty("tip", out var tip) && tip.ValueKind == JsonValueKind.String
                ? tip.GetString()
                : null);
```

En `ValidadorDeTabla.ValidarSpot`, justo después del `if (claveSpotDeclarada is null) Anotar(...)`:

```csharp
        // El tip es opcional, pero declararlo vacío es casi siempre un guardado
        // a medias: mejor que se vea en pantalla a que se pierda en silencio.
        if (spot.TryGetProperty("tip", out var tip)
            && (tip.ValueKind != JsonValueKind.String
                || string.IsNullOrWhiteSpace(tip.GetString())))
            Anotar("El 'tip' del spot está vacío. Sacá la clave o escribí algo.");
```

En `AnalizadorDeMemoria.Analizar`, cambiar el último argumento de `null` a `spot.Tip`.

- [ ] **Step 4: Run tests to verify they pass**

Run: `dotnet test PokerProOS.slnx --filter "FullyQualifiedName~Tablas"`
Expected: PASS. Los tests viejos que construyen `SpotDeTabla` con tres argumentos siguen compilando: el cuarto es opcional.

- [ ] **Step 5: Commit**

```bash
git add src/PokerProOS.Application/Tablas/ICatalogoDeTablas.cs src/PokerProOS.Infrastructure/Tablas/CargadorDeTablas.cs src/PokerProOS.Infrastructure/Tablas/ValidadorDeTabla.cs src/PokerProOS.Application/Tablas/AnalizadorDeMemoria.cs tests/PokerProOS.Tests/Tablas/
git commit -m "feat: el spot puede declarar un tip escrito a mano"
```

---

### Task 7: Guardar el tip con `IEditorDeTablas`

**Files:**
- Modify: `src/PokerProOS.Application/Tablas/IEditorDeTablas.cs`
- Modify: `src/PokerProOS.Infrastructure/Tablas/EditorDeTablasJson.cs`
- Test: `tests/PokerProOS.Tests/Tablas/EditorDeTipTests.cs` (crear)

**Interfaces:**
- Consumes: `EditorDeTablasJson(string directorio, CatalogoVivo catalogo, CargadorDeTablas cargador)` y sus privados `UbicarArchivo` / `UbicarSpot`; `ResultadoDeEdicion`.
- Produces: `record EdicionDeTip(string Situacion, string ClaveDeStack, string Spot, string? Texto)` y `Task<ResultadoDeEdicion> IEditorDeTablas.EditarTipAsync(EdicionDeTip edicion, CancellationToken ct)`.

- [ ] **Step 1: Write the failing test**

Crear `tests/PokerProOS.Tests/Tablas/EditorDeTipTests.cs`. Copia las semillas a un temporal para no ensuciar `database/`:

```csharp
using PokerProOS.Application.Tablas;
using PokerProOS.Infrastructure.Tablas;

namespace PokerProOS.Tests.Tablas;

public class EditorDeTipTests : IDisposable
{
    private readonly string _directorio =
        Path.Combine(Path.GetTempPath(), "tips-" + Guid.NewGuid().ToString("N"));

    private readonly EditorDeTablasJson _editor;
    private readonly CatalogoVivo _catalogo;

    public EditorDeTipTests()
    {
        Directory.CreateDirectory(_directorio);
        foreach (var archivo in Directory.GetFiles(Rutas.SemillasDeTablas, "*.json"))
            File.Copy(archivo, Path.Combine(_directorio, Path.GetFileName(archivo)));

        var acciones = RegistroDeAccionesJson.Cargar(Rutas.Registro("acciones.json"));
        var cargador = new CargadorDeTablas(new ValidadorDeTabla(acciones), acciones);
        _catalogo = new CatalogoVivo(cargador.CargarDirectorio(_directorio));
        _editor = new EditorDeTablasJson(_directorio, _catalogo, cargador);
    }

    public void Dispose()
    {
        Directory.Delete(_directorio, recursive: true);
        GC.SuppressFinalize(this);
    }

    [Fact]
    public async Task Guarda_el_tip_y_recarga_el_catalogo()
    {
        var resultado = await _editor.EditarTipAsync(new EdicionDeTip(
            "HU_SB_OR_FISH", "17-18bb", "SB_OR", "Los ases bajos suben por el color."), default);

        Assert.True(resultado.Exito);
        Assert.Equal(
            "Los ases bajos suben por el color.",
            _catalogo.Spot("HU_SB_OR_FISH", "17-18bb", "SB_OR")!.Tip);
    }

    [Fact]
    public async Task Un_texto_vacio_borra_el_tip()
    {
        await _editor.EditarTipAsync(new EdicionDeTip(
            "HU_SB_OR_FISH", "17-18bb", "SB_OR", "algo"), default);
        var resultado = await _editor.EditarTipAsync(new EdicionDeTip(
            "HU_SB_OR_FISH", "17-18bb", "SB_OR", "   "), default);

        Assert.True(resultado.Exito);
        Assert.Null(_catalogo.Spot("HU_SB_OR_FISH", "17-18bb", "SB_OR")!.Tip);
        // Y la clave no queda vacía en el archivo, que sería un ProblemaDeTabla.
        Assert.Empty(resultado.Problemas);
    }

    [Fact]
    public async Task Avisa_cuando_el_spot_no_existe()
    {
        var resultado = await _editor.EditarTipAsync(new EdicionDeTip(
            "HU_SB_OR_FISH", "17-18bb", "NO_EXISTE", "x"), default);

        Assert.False(resultado.Exito);
        Assert.NotNull(resultado.Error);
    }
}
```

Antes de correrlo, abrir `src/PokerProOS.Application/Tablas/CatalogoVivo.cs` y confirmar la firma del constructor y el nombre del método de lectura (`Spot`); ajustar si difieren.

- [ ] **Step 2: Run test to verify it fails**

Run: `dotnet test PokerProOS.slnx --filter "FullyQualifiedName~EditorDeTipTests"`
Expected: FAIL — no compila: no existen `EditarTipAsync` ni `EdicionDeTip`.

- [ ] **Step 3: Write minimal implementation**

En `src/PokerProOS.Application/Tablas/IEditorDeTablas.cs`, agregar el record al lado de `EdicionDeCelda`:

```csharp
/// <summary>
/// El porqué escrito a mano de un spot. Texto vacío o nulo borra la clave del
/// archivo: un "tip" en blanco es un problema de tabla, no un tip.
/// </summary>
public record EdicionDeTip(string Situacion, string ClaveDeStack, string Spot, string? Texto);
```

y el método a la interfaz:

```csharp
    Task<ResultadoDeEdicion> EditarTipAsync(EdicionDeTip edicion, CancellationToken ct);
```

En `EditorDeTablasJson` hacen falta dos refactors chicos **que no cambian comportamiento**, para poder compartir el camino de escritura:

1. `UbicarSpot(JsonObject raiz, EdicionDeCelda edicion)` pasa a `UbicarSpot(JsonObject raiz, string claveDeStack, string spot)` — reemplazar `edicion.ClaveDeStack` / `edicion.Spot` por los parámetros adentro, y en `EditarAsync` llamar `UbicarSpot(raiz, edicion.ClaveDeStack, edicion.Spot)`.
2. Extraer el bloque final de `EditarAsync` (escribir a temporal, mover, recargar, devolver) a:

```csharp
    /// <summary>
    /// Escribe a un temporal y mueve: si el proceso muere a mitad de camino, el
    /// archivo original queda intacto en vez de truncado. Después recarga el
    /// catálogo, para que la pantalla vea el cambio sin reiniciar.
    /// </summary>
    private async Task<ResultadoDeEdicion> GuardarYRecargar(
        string archivo, JsonObject raiz, CancellationToken ct)
    {
        var temporal = archivo + ".tmp";
        await File.WriteAllTextAsync(temporal,
            raiz.ToJsonString(new JsonSerializerOptions { WriteIndented = true }), ct);
        File.Move(temporal, archivo, overwrite: true);

        var recargado = cargador.CargarDirectorio(directorio);
        catalogo.Reemplazar(recargado);
        return new ResultadoDeEdicion(true, null, recargado.Problemas);
    }
```

`EditarAsync` termina entonces con `return await GuardarYRecargar(archivo, raiz, ct);`.

Y agregar el método nuevo, que reusa el mismo semáforo:

```csharp
    public async Task<ResultadoDeEdicion> EditarTipAsync(EdicionDeTip edicion, CancellationToken ct)
    {
        await _turno.WaitAsync(ct);
        try
        {
            var archivo = UbicarArchivo(edicion.Situacion, edicion.ClaveDeStack);
            if (archivo is null)
                return new ResultadoDeEdicion(false,
                    $"No encontré ningún archivo con {edicion.Situacion} / {edicion.ClaveDeStack}.", []);

            var raiz = JsonNode.Parse(await File.ReadAllTextAsync(archivo, ct))!.AsObject();
            var spot = UbicarSpot(raiz, edicion.ClaveDeStack, edicion.Spot);
            if (spot is null)
                return new ResultadoDeEdicion(false,
                    $"No encontré {edicion.ClaveDeStack}/{edicion.Spot} en ese archivo.", []);

            if (string.IsNullOrWhiteSpace(edicion.Texto)) spot.Remove("tip");
            else spot["tip"] = edicion.Texto.Trim();

            return await GuardarYRecargar(archivo, raiz, ct);
        }
        finally
        {
            _turno.Release();
        }
    }
```

- [ ] **Step 4: Run tests to verify they pass**

Run: `dotnet test PokerProOS.slnx --filter "FullyQualifiedName~Tablas"`
Expected: PASS, incluidos los tests de edición de celda que ya existían — el refactor no debe moverlos.

- [ ] **Step 5: Commit**

```bash
git add src/PokerProOS.Application/Tablas/IEditorDeTablas.cs src/PokerProOS.Infrastructure/Tablas/EditorDeTablasJson.cs tests/PokerProOS.Tests/Tablas/EditorDeTipTests.cs
git commit -m "feat: guardar el tip de un spot desde el editor de tablas"
```

---

### Task 8: La voz deja de decir «en el borde»

**Files:**
- Modify: `src/PokerProOS.Application/Voz/RedactorDeRespuesta.cs` (las líneas `if (r.EnElBorde)` / `frase += ...`)
- Test: `tests/PokerProOS.Tests/Voz/RedactorDeRespuestaTests.cs`

**Interfaces:**
- Consumes: `RespuestaDeMano` sin cambios — `EnElBorde` y `ManosEnLaAccion` siguen existiendo porque la pantalla los usa para resaltar.
- Produces: nada nuevo.

- [ ] **Step 1: Write the failing test**

Buscar en `RedactorDeRespuestaTests.cs` el o los tests que afirman que la frase menciona el borde y **reemplazarlos** por éste, adaptando el helper de construcción que ya use el archivo:

```csharp
    [Fact]
    public void No_habla_del_borde_del_rango()
    {
        // "En el borde, N manos" contaba casillas de la grilla y no decía
        // contra qué limita: eso ahora se lee en la ficha, no se escucha.
        var frase = Redactor().Redactar(new ResultadoDeConsulta(
            new RespuestaDeMano("A8o", "CALL", 82, EnElBorde: true, PaloAsumido: false, "17-18bb"),
            null, null));

        Assert.Equal("CALL.", frase);
        Assert.DoesNotContain("borde", frase, StringComparison.OrdinalIgnoreCase);
    }
```

- [ ] **Step 2: Run test to verify it fails**

Run: `dotnet test PokerProOS.slnx --filter "FullyQualifiedName~RedactorDeRespuestaTests"`
Expected: FAIL — la frase es `"CALL. En el borde, 82 manos."`.

- [ ] **Step 3: Write minimal implementation**

En `RedactorDeRespuesta.Redactar`, borrar estas dos líneas:

```csharp
        if (r.EnElBorde)
            frase += $" En el borde, {r.ManosEnLaAccion} manos.";
```

de modo que `return frase;` quede inmediatamente después de asignar `frase`.

- [ ] **Step 4: Run tests to verify they pass**

Run: `dotnet test PokerProOS.slnx --filter "FullyQualifiedName~Redactor"`
Expected: PASS — también los de mix, que no tocan esa rama.

- [ ] **Step 5: Commit**

```bash
git add src/PokerProOS.Application/Voz/RedactorDeRespuesta.cs tests/PokerProOS.Tests/Voz/RedactorDeRespuestaTests.cs
git commit -m "fix: la voz ya no dice en el borde"
```

---

### Task 9: La ficha viaja en el evento del copiloto

**Files:**
- Modify: `src/PokerProOS.Application/Voz/CopilotoDeVoz.cs`
- Modify: `src/PokerProOS.Api/Program.cs`
- Test: `tests/PokerProOS.Tests/Voz/CopilotoDeVozTests.cs`

**Interfaces:**
- Consumes: `AnalizadorDeMemoria.Analizar` (Tasks 2-6), `FichaDeMemoria`.
- Produces: `EventoDeCopiloto` gana un último parámetro `FichaDeMemoria? Ficha = null`; `CopilotoDeVoz` gana un parámetro de constructor `AnalizadorDeMemoria analizador` **al final**, para no alterar el orden de los que ya están.

- [ ] **Step 1: Write the failing test**

En `tests/PokerProOS.Tests/Voz/CopilotoDeVozTests.cs`, adaptando los helpers que ya tenga el archivo (mirar `DoblesDeVoz.cs` y cómo se arma un `DictadoReconocido`):

```csharp
    [Fact]
    public void El_evento_trae_la_ficha_de_la_mano_resuelta()
    {
        var evento = Copiloto().Procesar(Dictado("17-18bb", "SB_OR", "A", "8", null));

        Assert.NotNull(evento.Ficha);
        Assert.Equal("A8o", evento.Ficha!.Mano);
        Assert.Equal("CALL", evento.Ficha.Accion);
        Assert.NotEmpty(evento.Ficha.Umbral);
    }

    [Fact]
    public void Un_dictado_que_no_resuelve_no_trae_ficha()
    {
        var evento = Copiloto().Procesar(Dictado("17-18bb", "SB_OR", "X", "8", null));
        Assert.Null(evento.Ficha);
    }
```

El helper `Copiloto()` que ya existe hay que ampliarlo para que construya también el `AnalizadorDeMemoria` sobre el mismo catálogo que ya arma para `ResolverManoHandler`.

- [ ] **Step 2: Run test to verify it fails**

Run: `dotnet test PokerProOS.slnx --filter "FullyQualifiedName~CopilotoDeVozTests"`
Expected: FAIL — no compila: `EventoDeCopiloto` no tiene `Ficha`.

- [ ] **Step 3: Write minimal implementation**

En `CopilotoDeVoz.cs`, agregar el campo al final del record:

```csharp
public record EventoDeCopiloto(
    string TextoCrudo,
    string ManoInterpretada,
    string Accion,
    string Respuesta,
    bool Resuelta,
    string? Situacion,
    string? ClaveDeStack,
    string? Spot,
    /// <summary>
    /// Lo que hay que saber de esa mano, para leer. Nulo si el dictado no
    /// resolvió: no hay nada que explicar de una mano que no se entendió.
    /// </summary>
    FichaDeMemoria? Ficha = null);
```

Agregar `AnalizadorDeMemoria analizador` al final de los parámetros del constructor primario de `CopilotoDeVoz`. En `Procesar`, después de `var resultado = resolver.Resolver(...)`:

```csharp
        // Resolver y explicar son dos cosas distintas: ResolverManoHandler
        // sigue respondiendo "qué hago" y el analizador agrega el "por qué".
        var ficha = resultado.Respuesta is null
            ? null
            : analizador.Analizar(
                memoria.Situacion,
                resultado.Respuesta.ClaveDeStack,
                memoria.Spot,
                resultado.Respuesta.Mano);
```

y pasar `ficha` como último argumento del `new EventoDeCopiloto(...)`.

En `Program.cs`: registrar `AnalizadorDeMemoria` junto a los otros handlers, construido con el **catálogo vivo** (`CatalogoVivo`) —el mismo que ya recibe `ResolverManoHandler`— para que una tabla corregida se refleje sin reiniciar, y pasarlo donde se construye `CopilotoDeVoz`.

- [ ] **Step 4: Run tests to verify they pass**

Run: `dotnet test PokerProOS.slnx --filter "FullyQualifiedName~CopilotoDeVozTests"` y después `dotnet build PokerProOS.slnx -p:SaltearFrontend=true`
Expected: PASS y build limpio — el build es lo que verifica que `Program.cs` quedó bien.

- [ ] **Step 5: Commit**

```bash
git add src/PokerProOS.Application/Voz/CopilotoDeVoz.cs src/PokerProOS.Api/Program.cs tests/PokerProOS.Tests/Voz/CopilotoDeVozTests.cs
git commit -m "feat: el evento del copiloto lleva la ficha de memoria"
```

---

### Task 10: Endpoints de ficha y de tip

**Files:**
- Modify: `src/PokerProOS.Api/Controllers/TablasController.cs`

**Interfaces:**
- Consumes: `AnalizadorDeMemoria.Analizar`, `IEditorDeTablas.EditarTipAsync`, `EdicionDeTip`.
- Produces:
  - `GET /api/tablas/ficha?situacion=&stack=&spot=&mano=` → `200` con el `FichaDeMemoria` serializado, o `404 { error }`.
  - `PUT /api/tablas/{situacion}/{stack}/{spot}/tip` con cuerpo `{ "texto": string | null }` → `200 { problemas }` o `400 { error }`.

No hay tests de controller en este proyecto (la lógica vive en Application y ya está cubierta); la verificación de esta tarea es el build más las dos llamadas manuales del Step 4.

- [ ] **Step 1: Escribir los endpoints**

En `TablasController.cs`, agregar el record al lado de `CeldaEnviada`:

```csharp
public record TipEnviado(string? Texto);
```

Agregar `AnalizadorDeMemoria analizador` a los parámetros del constructor primario del controller, y estos dos métodos:

```csharp
    /// <summary>
    /// Todo lo que hay que saber de una mano en un spot. Existe aparte del
    /// evento de voz para poder estudiar tocando la grilla, sin micrófono.
    /// </summary>
    [HttpGet("ficha")]
    public IActionResult Ficha(
        [FromQuery] string situacion, [FromQuery] string stack,
        [FromQuery] string spot, [FromQuery] string mano)
    {
        var ficha = analizador.Analizar(situacion, stack, spot, mano);
        return ficha is null
            ? NotFound(new { error = $"No tengo ficha de {mano} en {stack}/{spot}." })
            : Ok(ficha);
    }

    /// <summary>
    /// El porqué escrito a mano. Como la edición de celda, escribe el JSON —la
    /// fuente de verdad— y recarga el catálogo en caliente.
    /// </summary>
    [HttpPut("{situacion}/{stack}/{spot}/tip")]
    public async Task<IActionResult> EditarTip(
        string situacion, string stack, string spot,
        [FromBody] TipEnviado enviado, CancellationToken ct)
    {
        var resultado = await editor.EditarTipAsync(
            new EdicionDeTip(situacion, stack, spot, enviado.Texto), ct);

        return resultado.Exito
            ? Ok(new { problemas = resultado.Problemas })
            : BadRequest(new { error = resultado.Error });
    }
```

**Ojo con el ruteo:** `PUT .../{spot}/tip` tiene cuatro segmentos, igual que `PUT .../{spot}/{mano}` de `EditarCelda`. ASP.NET resuelve a favor del literal `tip`, así que no chocan — pero significa que **nunca puede existir una mano llamada `tip`**, cosa que la matriz de 169 garantiza. `GET ficha` tiene un solo segmento y hoy no compite con nada.

- [ ] **Step 2: Compilar**

Run: `dotnet build PokerProOS.slnx -p:SaltearFrontend=true`
Expected: build limpio.

- [ ] **Step 3: Correr la suite de tablas**

Run: `dotnet test PokerProOS.slnx --filter "FullyQualifiedName~Tablas"`
Expected: PASS.

- [ ] **Step 4: Verificar los dos endpoints a mano**

Levantar: `dotnet run --project src/PokerProOS.Api`

```bash
curl "http://localhost:5000/api/tablas/ficha?situacion=HU_SB_OR_FISH&stack=17-18bb&spot=SB_OR&mano=A8o"
```
Expected: JSON con `accion: "CALL"`, `pesos` con tres entradas, `ancla` no nulo, `umbral` con tres bandas, `familias` con tres, `linea` con cinco pasos.

```bash
curl -X PUT "http://localhost:5000/api/tablas/HU_SB_OR_FISH/17-18bb/SB_OR/tip" \
  -H "Content-Type: application/json" -d '{"texto":"Prueba"}'
curl "http://localhost:5000/api/tablas/ficha?situacion=HU_SB_OR_FISH&stack=17-18bb&spot=SB_OR&mano=A8o"
```
Expected: el segundo `curl` trae `"tip": "Prueba"`. Después dejar el archivo como estaba:

```bash
curl -X PUT "http://localhost:5000/api/tablas/HU_SB_OR_FISH/17-18bb/SB_OR/tip" \
  -H "Content-Type: application/json" -d '{"texto":null}'
git status --short database/
```
Expected: `git status` sin cambios en `database/`.

- [ ] **Step 5: Commit**

```bash
git add src/PokerProOS.Api/Controllers/TablasController.cs
git commit -m "feat: endpoints de ficha de memoria y de tip"
```

---

### Task 11: Modelo y servicio del frontend

**Files:**
- Modify: `frontend/src/core/models/catalogo.model.ts`
- Modify: `frontend/src/core/services/tablasApi.ts`

**Interfaces:**
- Consumes: la forma JSON que serializa `FichaDeMemoria` (camelCase, por la convención por defecto de ASP.NET).
- Produces: `interface FichaDeMemoria` y sus partes, `obtenerFicha(situacion, stack, spot, mano)`, `guardarTip(situacion, stack, spot, texto)`, y `EventoDeVoz.ficha`.

- [ ] **Step 1: Escribir los tipos**

En `frontend/src/core/models/catalogo.model.ts`, después de `SpotCompleto`:

```typescript
export interface PesoDeAccion {
  accion: string
  combos: number
  porcentajeDeBaraja: number
}

/** El bloque de una familia que comparte acción, y la mano que lo rompe. */
export interface AnclaDeFamilia {
  familia: string
  tope: string
  fondo: string
  accion: string
  siguiente: string | null
  accionSiguiente: string | null
}

export interface BandaDeStack {
  claveDeStack: string
  minBB: number
  maxBB: number
  accion: string
  /** Si el stack consultado cae adentro de esta banda. */
  esElActual: boolean
}

export interface PasoDeLinea {
  spot: string
  etiqueta: string
  accion: string
  esElConsultado: boolean
}

export interface FichaDeMemoria {
  mano: string
  accion: string
  claveDeStack: string
  pesos: PesoDeAccion[]
  ancla: AnclaDeFamilia | null
  umbral: BandaDeStack[]
  familias: AnclaDeFamilia[]
  linea: PasoDeLinea[]
  tip: string | null
}
```

Y agregar el campo a `EventoDeVoz`:

```typescript
  ficha: FichaDeMemoria | null
```

- [ ] **Step 2: Escribir el servicio**

En `frontend/src/core/services/tablasApi.ts`, agregar `FichaDeMemoria` al `import type` de arriba y, debajo de `editarCelda`:

```typescript
export const obtenerFicha = (situacion: string, stack: string, spot: string, mano: string) =>
  pedir<FichaDeMemoria>(
    `/api/tablas/ficha?situacion=${encodeURIComponent(situacion)}`
    + `&stack=${encodeURIComponent(stack)}`
    + `&spot=${encodeURIComponent(spot)}`
    + `&mano=${encodeURIComponent(mano)}`)

export async function guardarTip(
  situacion: string, stack: string, spot: string, texto: string | null,
): Promise<void> {
  const respuesta = await fetch(
    `/api/tablas/${situacion}/${stack}/${spot}/tip`,
    {
      method: 'PUT',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify({ texto }),
    },
  )
  if (!respuesta.ok) {
    const error = await respuesta.json().catch(() => null) as { error?: string } | null
    throw new Error(error?.error ?? `${respuesta.status} ${respuesta.statusText}`)
  }
}
```

- [ ] **Step 3: Verificar que compila y lintea**

Run: `cd frontend && npm run build && npm run lint`
Expected: `tsc -b` sin errores y oxlint limpio.

- [ ] **Step 4: Commit**

```bash
git add frontend/src/core/models/catalogo.model.ts frontend/src/core/services/tablasApi.ts
git commit -m "feat: modelo y servicio de la ficha de memoria"
```

---

### Task 12: El popup `FichaDeMemoria.tsx`

**Files:**
- Create: `frontend/src/features/tablas/FichaDeMemoria.tsx`
- Modify: `frontend/src/index.css`

**Interfaces:**
- Consumes: `FichaDeMemoria` (modelo) y `AccionDefinida` (Task 11).
- Produces: el componente `FichaDeMemoria` con estas props exactas — `children` es el hueco donde la Task 13 mete el `EditorDeCelda`:

```typescript
interface Props {
  ficha: FichaModelo
  acciones: AccionDefinida[]
  guardandoTip: boolean
  errorAlGuardarTip: string | null
  onGuardarTip: (texto: string | null) => void
  onCerrar: () => void
  children?: ReactNode
}
```

- [ ] **Step 1: Escribir el componente**

Crear `frontend/src/features/tablas/FichaDeMemoria.tsx`:

```tsx
import { useEffect, useState, type ReactNode } from 'react'
import type { AccionDefinida, FichaDeMemoria as FichaModelo } from '../../core/models/catalogo.model'

interface Props {
  ficha: FichaModelo
  acciones: AccionDefinida[]
  guardandoTip: boolean
  errorAlGuardarTip: string | null
  onGuardarTip: (texto: string | null) => void
  onCerrar: () => void
  children?: ReactNode
}

/**
 * Todo lo que se sabe de una mano, en un popup: la casilla con su color y
 * después las relaciones que sirven para memorizarla — hasta dónde llega su
 * familia, desde qué stack cambia, cuánta baraja se lleva y qué pasa después.
 * Reemplaza el "en el borde, N manos" que se hablaba y no decía contra qué.
 */
export function FichaDeMemoria({
  ficha, acciones, guardandoTip, errorAlGuardarTip, onGuardarTip, onCerrar, children,
}: Props) {
  const porClave = new Map(acciones.map((a) => [a.clave, a]))
  const etiqueta = (clave: string) => porClave.get(clave)?.etiqueta ?? clave
  const pintar = (clave: string) => {
    const accion = porClave.get(clave)
    return accion ? { background: accion.color, color: accion.colorTexto } : undefined
  }

  const [editandoTip, setEditandoTip] = useState(false)
  const [borrador, setBorrador] = useState(ficha.tip ?? '')

  // Cambiar de mano sin cerrar el popup (dictando otra) tiene que traer el tip
  // de la nueva, no dejar el borrador de la anterior a medio escribir.
  useEffect(() => {
    // oxlint-disable-next-line set-state-in-effect
    setEditandoTip(false)
    setBorrador(ficha.tip ?? '')
  }, [ficha.mano, ficha.claveDeStack, ficha.tip])

  useEffect(() => {
    const alTeclear = (e: KeyboardEvent) => { if (e.key === 'Escape') onCerrar() }
    window.addEventListener('keydown', alTeclear)
    return () => window.removeEventListener('keydown', alTeclear)
  }, [onCerrar])

  const miPeso = ficha.pesos.find((p) => p.accion === ficha.accion)

  return (
    // El click del fondo cierra; adentro se frena, o cerraría al tocar cualquier cosa.
    <div className="ficha-fondo" onClick={onCerrar} role="presentation">
      <div
        className="ficha-popup"
        role="dialog"
        aria-label={`Ficha de ${ficha.mano}`}
        onClick={(e) => e.stopPropagation()}
      >
        <header className="ficha-cabecera">
          <span className="ficha-casilla" style={pintar(ficha.accion)}>{ficha.mano}</span>
          <div className="ficha-titulo">
            <strong>{etiqueta(ficha.accion)}</strong>
            <span className="ficha-stack">{ficha.claveDeStack}</span>
          </div>
          {miPeso && (
            <span className="ficha-peso">
              {miPeso.porcentajeDeBaraja.toFixed(1)}% de la baraja
            </span>
          )}
          <button type="button" className="boton-tenue" onClick={onCerrar}>Cerrar</button>
        </header>

        {ficha.ancla && (
          <section className="ficha-bloque">
            <h3>Ancla</h3>
            <p>
              En <strong>{ficha.ancla.familia}</strong>, de <strong>{ficha.ancla.tope}</strong>{' '}
              hasta <strong>{ficha.ancla.fondo}</strong> va{' '}
              <span className="ficha-chip" style={pintar(ficha.ancla.accion)}>
                {etiqueta(ficha.ancla.accion)}
              </span>
              {ficha.ancla.siguiente && ficha.ancla.accionSiguiente && (
                <>
                  {'. '}Desde <strong>{ficha.ancla.siguiente}</strong> ya es{' '}
                  <span className="ficha-chip" style={pintar(ficha.ancla.accionSiguiente)}>
                    {etiqueta(ficha.ancla.accionSiguiente)}
                  </span>
                </>
              )}.
            </p>
          </section>
        )}

        {ficha.umbral.length > 0 && (
          <section className="ficha-bloque">
            <h3>Según el stack</h3>
            <ul className="ficha-umbral">
              {ficha.umbral.map((banda) => (
                <li
                  key={banda.claveDeStack}
                  className={banda.esElActual ? 'ficha-banda-actual' : ''}
                >
                  <span className="ficha-banda-stack">{banda.minBB}–{banda.maxBB}bb</span>
                  <span className="ficha-chip" style={pintar(banda.accion)}>
                    {etiqueta(banda.accion)}
                  </span>
                </li>
              ))}
            </ul>
          </section>
        )}

        {ficha.familias.length > 0 && (
          <section className="ficha-bloque">
            <h3>Hasta dónde llega cada familia</h3>
            <ul className="ficha-familias">
              {ficha.familias.map((f) => (
                <li key={f.familia}>
                  <strong>{f.familia}</strong>
                  <span className="ficha-chip" style={pintar(f.accion)}>{etiqueta(f.accion)}</span>
                  hasta <strong>{f.fondo}</strong>
                  {f.siguiente && f.accionSiguiente && (
                    <>
                      {' · '}{f.siguiente}{' '}
                      <span className="ficha-chip" style={pintar(f.accionSiguiente)}>
                        {etiqueta(f.accionSiguiente)}
                      </span>
                    </>
                  )}
                </li>
              ))}
            </ul>
          </section>
        )}

        {ficha.linea.length > 1 && (
          <section className="ficha-bloque">
            <h3>Y después</h3>
            <ol className="ficha-linea">
              {ficha.linea.map((paso) => (
                <li key={paso.spot} className={paso.esElConsultado ? 'ficha-paso-actual' : ''}>
                  <span className="ficha-paso-spot">{paso.etiqueta}</span>
                  <span className="ficha-chip" style={pintar(paso.accion)}>
                    {etiqueta(paso.accion)}
                  </span>
                </li>
              ))}
            </ol>
          </section>
        )}

        <section className="ficha-bloque">
          <h3>Tip</h3>
          {editandoTip ? (
            <div className="ficha-tip-editor">
              <textarea
                value={borrador}
                rows={3}
                placeholder="Por qué esta tabla hace lo que hace"
                onChange={(e) => setBorrador(e.target.value)}
              />
              <div className="ficha-tip-botones">
                <button
                  type="button"
                  disabled={guardandoTip}
                  onClick={() => onGuardarTip(borrador.trim() === '' ? null : borrador)}
                >
                  {guardandoTip ? 'Guardando…' : 'Guardar'}
                </button>
                <button
                  type="button"
                  className="boton-tenue"
                  onClick={() => { setEditandoTip(false); setBorrador(ficha.tip ?? '') }}
                >
                  Cancelar
                </button>
              </div>
              {errorAlGuardarTip && <p className="error">{errorAlGuardarTip}</p>}
            </div>
          ) : (
            <div className="ficha-tip">
              {ficha.tip
                ? <p>{ficha.tip}</p>
                : <p className="ficha-tip-vacio">Todavía no escribiste el porqué de esta tabla.</p>}
              <button type="button" className="boton-tenue" onClick={() => setEditandoTip(true)}>
                {ficha.tip ? 'Editar' : 'Escribir'}
              </button>
            </div>
          )}
        </section>

        {children}
      </div>
    </div>
  )
}
```

- [ ] **Step 2: Escribir los estilos**

En `frontend/src/index.css`, entre el bloque `/* ---------- Editor de celda ---------- */` y `/* ---------- Vocabulario de voz ---------- */`:

```css
/* ---------- Ficha de memoria ---------- */

.ficha-fondo {
  position: fixed; inset: 0; z-index: 40;
  display: flex; align-items: flex-start; justify-content: center;
  padding: 40px 16px; overflow-y: auto;
  background: rgba(4, 8, 14, .72);
}
.ficha-popup {
  width: 100%; max-width: 560px; padding: 16px 18px 18px;
  border: 1px solid var(--acento); border-radius: 12px; background: var(--panel);
  box-shadow: 0 18px 48px rgba(0, 0, 0, .55);
}
.ficha-cabecera { display: flex; align-items: center; gap: 12px; margin-bottom: 4px; }

/* El mismo cuadro que en la grilla: el ojo lo reconoce sin leer. */
.ficha-casilla {
  display: grid; place-items: center; width: 54px; height: 54px;
  border-radius: 8px; font-size: 15px; font-weight: 800; letter-spacing: .03em;
}
.ficha-titulo { display: grid; gap: 2px; }
.ficha-titulo strong { font-size: 16px; letter-spacing: .04em; }
.ficha-stack { color: var(--apagado); font-size: 12px; }
.ficha-peso {
  margin-left: auto; color: var(--apagado);
  font-size: 12px; font-variant-numeric: tabular-nums;
}

.ficha-bloque { margin-top: 15px; padding-top: 13px; border-top: 1px solid var(--borde); }
.ficha-bloque h3 {
  margin: 0 0 8px; color: var(--apagado);
  font-size: 11px; font-weight: 700; letter-spacing: .09em; text-transform: uppercase;
}
.ficha-bloque p { margin: 0; font-size: 13px; line-height: 1.65; }

.ficha-chip {
  display: inline-block; padding: 2px 7px; border-radius: 5px;
  font-size: 11px; font-weight: 800; letter-spacing: .02em; vertical-align: baseline;
}

.ficha-umbral, .ficha-familias, .ficha-linea {
  margin: 0; padding: 0; list-style: none; display: grid; gap: 6px;
}
.ficha-umbral li, .ficha-linea li { display: flex; align-items: center; gap: 9px; font-size: 13px; }
.ficha-familias li {
  display: flex; align-items: center; gap: 7px; font-size: 13px; flex-wrap: wrap;
}
.ficha-banda-stack, .ficha-paso-spot {
  min-width: 96px; color: var(--apagado); font-variant-numeric: tabular-nums;
}
.ficha-banda-actual, .ficha-paso-actual { font-weight: 700; }
.ficha-banda-actual .ficha-banda-stack, .ficha-paso-actual .ficha-paso-spot { color: var(--texto); }

.ficha-tip { display: flex; align-items: flex-start; gap: 10px; }
.ficha-tip p { flex: 1; font-size: 13px; line-height: 1.65; }
.ficha-tip-vacio { color: var(--apagado); font-style: italic; }
.ficha-tip-editor { display: grid; gap: 8px; }
.ficha-tip-editor textarea {
  width: 100%; resize: vertical;
  background: var(--panel-2); color: var(--texto);
  border: 1px solid var(--borde); border-radius: 6px;
  padding: 8px 10px; font: inherit; font-size: 13px; line-height: 1.6;
}
.ficha-tip-botones { display: flex; gap: 7px; }
.ficha-tip-botones button:first-child {
  padding: 7px 13px; border-radius: 6px; border: 1px solid var(--acento);
  background: var(--panel-2); color: var(--texto);
  font: inherit; font-size: 12px; font-weight: 700; cursor: pointer;
}
.ficha-tip-botones button:disabled { opacity: .55; cursor: default; }

/* Adentro del popup el editor de celda no necesita su propio marco. */
.ficha-popup .editor-celda {
  margin-top: 15px; padding: 13px 0 0;
  border: 0; border-top: 1px solid var(--borde); border-radius: 0; background: none;
}
```

Verificar que `--panel`, `--panel-2`, `--acento`, `--apagado`, `--borde` y `--texto` existan en el `:root` de ese archivo; si alguno se llama distinto, usar el nombre real.

- [ ] **Step 3: Verificar que compila y lintea**

Run: `cd frontend && npm run build && npm run lint`
Expected: sin errores. El componente todavía no se usa; si oxlint marca el export sin uso, se resuelve en la Task 13.

- [ ] **Step 4: Commit**

```bash
git add frontend/src/features/tablas/FichaDeMemoria.tsx frontend/src/index.css
git commit -m "feat: popup de ficha de memoria"
```

---

### Task 13: Cablear el popup en la página

**Files:**
- Modify: `frontend/src/features/tablas/PaginaDeTablas.tsx`
- Modify: `frontend/src/features/tablas/Grilla.tsx` (sólo si la celda depende de recibir `onTocarCelda` para ser clickeable)

**Interfaces:**
- Consumes: `FichaDeMemoria` (componente y modelo), `obtenerFicha`, `guardarTip` (Tasks 11-12), `EditorDeCelda` (ya existe), `EventoDeVoz.ficha`.
- Produces: el comportamiento final. `EditorDeCelda` deja de renderizarse debajo de la grilla.

**Comportamiento buscado:**
1. Tocar una celda abre el popup con la ficha de esa mano — **siempre**, esté o no activo "Corregir tabla". Hoy `onTocarCelda` sólo se pasa cuando `editando` es `true`.
2. Dictar una mano abre el popup con esa ficha.
3. Con "Corregir tabla" activo, el `EditorDeCelda` se renderiza **adentro** del popup, como `children`.
4. Cerrar el popup limpia la mano abierta.
5. Guardar una celda o un tip recarga el spot **y** la ficha, para ver el cambio sin cerrar.

- [ ] **Step 1: Escribir los cambios**

Imports:

```tsx
import { FichaDeMemoria } from './FichaDeMemoria'
import type { FichaDeMemoria as FichaModelo } from '../../core/models/catalogo.model'
import { editarCelda, guardarTip, obtenerFicha, obtenerSpot } from '../../core/services/tablasApi'
```

Estado: renombrar `manoEnEdicion` → `manoAbierta` (sirve para las dos cosas ahora) y agregar:

```tsx
  const [manoAbierta, setManoAbierta] = useState<string | null>(null)
  const [ficha, setFicha] = useState<FichaModelo | null>(null)
  const [guardandoTip, setGuardandoTip] = useState(false)
  const [errorAlGuardarTip, setErrorAlGuardarTip] = useState<string | null>(null)
```

Traer la ficha cuando cambia la mano abierta o la tabla:

```tsx
  // La ficha se pide al backend en vez de derivarse de `datos`: las piezas que
  // la arman (umbral, familias) miran otros stacks y otros spots, que la
  // pantalla no tiene cargados.
  useEffect(() => {
    if (!manoAbierta || !situacion || !stack || !spot) {
      // oxlint-disable-next-line set-state-in-effect
      setFicha(null)
      return
    }
    let cancelado = false
    obtenerFicha(situacion, stack, spot, manoAbierta)
      .then((f) => { if (!cancelado) setFicha(f) })
      .catch(() => { if (!cancelado) setFicha(null) })
    return () => { cancelado = true }
  }, [situacion, stack, spot, manoAbierta, recarga])
```

En el `useEffect` que ya reacciona a `ultimo`, abrir el popup al dictar:

```tsx
    if (ultimo.manoInterpretada) setManoAbierta(ultimo.manoInterpretada)
```

En la `<Grilla>`, pasar siempre el handler:

```tsx
              <Grilla
                spot={datos}
                acciones={catalogo.acciones}
                manoResaltada={ultimo?.manoInterpretada || null}
                onTocarCelda={setManoAbierta}
              />
```

Abrir `Grilla.tsx` y revisar cómo usa `onTocarCelda`: si la celda sólo es un `<button>` con clase `celda-editable` cuando recibe el handler, ahora lo recibe siempre y queda clickeable siempre — que es lo buscado. Si además condiciona algo visual que sólo debería verse en modo edición, separar esa condición en una prop nueva `editable` y pasarle `editando`.

**Sacar** el bloque `{manoEnEdicion && (() => { … <EditorDeCelda … /> … })()}` de debajo de la grilla, y agregar el popup después del `</div>` de `entrenamiento-cuerpo`:

```tsx
      {ficha && (
        <FichaDeMemoria
          ficha={ficha}
          acciones={catalogo.acciones}
          guardandoTip={guardandoTip}
          errorAlGuardarTip={errorAlGuardarTip}
          onCerrar={() => { setManoAbierta(null); setErrorAlGuardarTip(null) }}
          onGuardarTip={(texto) => {
            setGuardandoTip(true)
            setErrorAlGuardarTip(null)
            guardarTip(situacion, stack, spot, texto)
              .then(() => setRecarga((n) => n + 1))
              .catch((e: unknown) =>
                setErrorAlGuardarTip(e instanceof Error ? e.message : 'No pude guardar el tip'))
              .finally(() => setGuardandoTip(false))
          }}
        >
          {editando && (() => {
            const celda = datos?.celdas.find((c) => c.mano === ficha.mano)
            if (!celda) return null
            return (
              <>
                {errorAlEditar && <p className="error">{errorAlEditar}</p>}
                <EditorDeCelda
                  celda={celda}
                  acciones={catalogo.acciones}
                  guardando={guardando}
                  onCerrar={() => setManoAbierta(null)}
                  onGuardar={(accion: string | null, mix: ParteDeMix[] | null) => {
                    setGuardando(true)
                    setErrorAlEditar(null)
                    editarCelda(situacion, stack, spot, ficha.mano, { accion, mix })
                      .then(() => setRecarga((n) => n + 1))
                      .catch((e: unknown) =>
                        setErrorAlEditar(e instanceof Error ? e.message : 'No pude guardar'))
                      .finally(() => setGuardando(false))
                  }}
                />
              </>
            )
          })()}
        </FichaDeMemoria>
      )}
```

`onGuardar` ya no cierra el popup (el `setManoEnEdicion(null)` que tenía desaparece): sube `recarga`, que recarga el spot y la ficha, y así se ve el cambio aplicado sin perder de vista la mano.

En el botón "Corregir tabla", sacar el `setManoEnEdicion(null)` del `onClick`: entrar o salir del modo edición no tiene por qué cerrar el popup.

- [ ] **Step 2: Verificar que compila y lintea**

Run: `cd frontend && npm run build && npm run lint`
Expected: sin errores. Si oxlint marca algún import que quedó sin uso, limpiarlo.

- [ ] **Step 3: Probarlo en la app**

Run: `dotnet build PokerProOS.slnx && dotnet run --project src/PokerProOS.Api`, abrir `http://localhost:5000`.

Verificar:
- Tocar `A8o` en `HU SB OR` / `17-18bb` / `SB_OR` abre el popup: casilla con el color de CALL, «de la baraja» en la cabecera, el ancla `Axo`, las tres bandas de stack, las tres familias y los cinco pasos de la línea.
- Escape, el botón Cerrar y el click en el fondo cierran.
- Escribir un tip, guardar, cerrar y volver a abrir: sigue ahí. Después borrarlo (guardar vacío) y confirmar con `git status --short database/` que el archivo quedó como estaba.
- Con "Corregir tabla" activo, el editor de acción/mix aparece adentro del popup y ya no debajo de la grilla.
- Encender la voz y dictar «diecisiete be be, as ocho»: el popup se abre solo con la ficha, y la respuesta hablada es «call», sin «en el borde».

- [ ] **Step 4: Commit**

```bash
git add frontend/src/features/tablas/PaginaDeTablas.tsx frontend/src/features/tablas/Grilla.tsx
git commit -m "feat: la grilla y la voz abren el popup de la ficha"
```

---

### Task 14: Cerrar — suite completa y CLAUDE.md

**Files:**
- Modify: `CLAUDE.md`

**Interfaces:**
- Consumes: todo lo anterior.
- Produces: nada de código.

- [ ] **Step 1: Correr toda la suite**

Run: `dotnet test PokerProOS.slnx`
Expected: PASS. Eran 114 tests; ahora deberían ser unos 135. Los de voz manejan audio real y son lentos: si alguno falla por falta de micrófono en el entorno, confirmarlo corriendo `dotnet test PokerProOS.slnx --filter "FullyQualifiedName!~ReconocedorSapi"` y **reportarlo**, no taparlo.

- [ ] **Step 2: Verificar que las tablas quedaron intactas**

Run: `git status --short database/`
Expected: sin cambios. Si aparece alguno, es un tip de prueba que quedó: revertir con `git checkout -- database/`.

- [ ] **Step 3: Documentar en CLAUDE.md**

En la sección **Chart JSON format**, agregar al final:

```markdown
Un spot puede además declarar `"tip"`: una frase escrita a mano con el porqué
estratégico de esa tabla. Es opcional, se edita desde el popup de la ficha (que
escribe el JSON vía `IEditorDeTablas`, igual que la corrección de celdas), y el
validador sólo se queja si la clave existe pero está vacía.
```

Después de la sección **The voice loop**, agregar:

```markdown
### La ficha de memoria

`AnalizadorDeMemoria` (Application, `Tablas/`) explica una mano en vez de sólo
responderla: deduce del catálogo la **mano ancla** de su familia (hasta dónde
llega el bloque que comparte su acción), el **umbral de stack** (la misma mano a
través de todos los stacks, colapsada en bandas), las **familias emparentadas**,
el **peso de baraja** de cada acción del spot —en combos, no en casillas: una
casilla suited son 4 manos reales y una offsuit 12— y **la línea** de spots del
stack en el orden en que ocurren. Todo sale del catálogo en memoria, así que
corregir una tabla cambia la explicación en el acto; la única pieza escrita a
mano es el `tip` del spot.

La ficha viaja en el `EventoDeCopiloto` (y por lo tanto en el SSE) y también se
pide sola en `GET /api/tablas/ficha`, para estudiar tocando la grilla sin
micrófono. La pantalla la muestra en un popup que además aloja el editor de
celda. La voz, en cambio, quedó corta a propósito: dice la acción y nada más.
```

- [ ] **Step 4: Commit**

```bash
git add CLAUDE.md
git commit -m "docs: la ficha de memoria en CLAUDE.md"
```
