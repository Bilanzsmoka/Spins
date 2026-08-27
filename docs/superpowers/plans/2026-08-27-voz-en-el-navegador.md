# La voz en el navegador — plan de implementación

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Reemplazar el reconocimiento y la síntesis de SAPI por la Web Speech API del navegador, dejando del lado del servidor un intérprete de texto puro que puede rechazar lo que no es una orden.

**Architecture:** El navegador oye y habla; el servidor entiende y responde. Chrome manda el texto reconocido a `POST /api/voz/dictado`; `InterpretadorDeTexto` lo convierte en el mismo `DictadoReconocido` que hoy arma la gramática SRGS, y de ahí para adentro nada cambia: `CopilotoDeVoz`, `MemoriaDeContexto`, `ResolverManoHandler` y la ficha siguen igual.

**Tech Stack:** .NET 10, xUnit, React 19 + TypeScript, Web Speech API (`SpeechRecognition` y `speechSynthesis`).

**Spec:** `docs/superpowers/specs/2026-08-27-voz-en-el-navegador-design.md`

## Global Constraints

- **Nada de listas en código.** Rangos, palos, spots, situaciones y palabras de stack salen siempre de `database/registro/vocabulario.json` vía `IRegistroDeVocabulario`. Las dos únicas constantes que el proyecto permite son los 13 rangos (`A K Q J T 9 8 7 6 5 4 3 2`) y el `169`, y ya viven en `MatrizDeManos`.
- **Comentarios en español**, explicando el *porqué* de lo que no es obvio, nunca el *qué*. Es la convención del repo.
- **Nombres en español**, como el resto del código (`InterpretadorDeTexto`, no `TextInterpreter`).
- **TDD**: test que falla, verlo fallar, implementación mínima, verlo pasar, commit.
- **Cultura `es-ES`**, el valor que ya usa `OpcionesDeVoz.Cultura`.
- El comando para las pruebas es `dotnet test PokerProOS.slnx -p:SaltearFrontend=true` desde la raíz.

---

### Task 1: `NumeroHablado` — números en palabras a entero

Chrome devuelve los números a veces en dígitos (`"9 bb"`) y a veces en letras (`"nueve be be"`), según cómo se dicte. El intérprete necesita las dos formas, y esto se prueba solo.

**Files:**
- Create: `src/PokerProOS.Application/Voz/NumeroHablado.cs`
- Test: `tests/PokerProOS.Tests/Voz/NumeroHabladoTests.cs`

**Interfaces:**
- Consumes: nada.
- Produces: `public static class NumeroHablado { public static int? Interpretar(string texto); }` — devuelve `null` si el texto no es un número entre 1 y 99.

- [ ] **Step 1: Write the failing test**

```csharp
using PokerProOS.Application.Voz;

namespace PokerProOS.Tests.Voz;

public class NumeroHabladoTests
{
    [Theory]
    [InlineData("9", 9)]
    [InlineData("15", 15)]
    [InlineData("nueve", 9)]
    [InlineData("quince", 15)]
    [InlineData("veinte", 20)]
    [InlineData("veintitres", 23)]
    [InlineData("treinta y cinco", 35)]
    [InlineData("noventa y nueve", 99)]
    [InlineData("uno", 1)]
    public void Interpreta_numeros_en_digitos_y_en_palabras(string texto, int esperado)
        => Assert.Equal(esperado, NumeroHablado.Interpretar(texto));

    [Theory]
    [InlineData("")]
    [InlineData("limp")]
    [InlineData("cuba")]
    [InlineData("0")]
    [InlineData("100")]
    [InlineData("ciento veinte")]
    public void Devuelve_nulo_cuando_no_es_un_numero_de_stack(string texto)
        => Assert.Null(NumeroHablado.Interpretar(texto));
}
```

- [ ] **Step 2: Run test to verify it fails**

Run: `dotnet test PokerProOS.slnx -p:SaltearFrontend=true --filter "FullyQualifiedName~NumeroHabladoTests"`
Expected: FAIL — `error CS0103: El nombre 'NumeroHablado' no existe`.

- [ ] **Step 3: Write minimal implementation**

```csharp
using System.Globalization;

namespace PokerProOS.Application.Voz;

/// <summary>
/// Convierte el número de un stack dictado a entero. Chrome devuelve a veces
/// dígitos ("9 bb") y a veces letras ("nueve be be") para la misma frase, así
/// que el intérprete tiene que entender las dos.
///
/// El rango útil es 1..99: es el que cubren las tablas, y acotarlo evita que
/// un año o un precio sueltos en una conversación pasen por stack.
/// </summary>
public static class NumeroHablado
{
    private static readonly Dictionary<string, int> Unidades = new()
    {
        ["uno"] = 1, ["dos"] = 2, ["tres"] = 3, ["cuatro"] = 4, ["cinco"] = 5,
        ["seis"] = 6, ["siete"] = 7, ["ocho"] = 8, ["nueve"] = 9, ["diez"] = 10,
        ["once"] = 11, ["doce"] = 12, ["trece"] = 13, ["catorce"] = 14, ["quince"] = 15,
        ["dieciseis"] = 16, ["diecisiete"] = 17, ["dieciocho"] = 18, ["diecinueve"] = 19,
        ["veinte"] = 20, ["veintiuno"] = 21, ["veintidos"] = 22, ["veintitres"] = 23,
        ["veinticuatro"] = 24, ["veinticinco"] = 25, ["veintiseis"] = 26,
        ["veintisiete"] = 27, ["veintiocho"] = 28, ["veintinueve"] = 29,
    };

    private static readonly Dictionary<string, int> Decenas = new()
    {
        ["treinta"] = 30, ["cuarenta"] = 40, ["cincuenta"] = 50, ["sesenta"] = 60,
        ["setenta"] = 70, ["ochenta"] = 80, ["noventa"] = 90,
    };

    public static int? Interpretar(string texto)
    {
        var limpio = (texto ?? "").Trim().ToLowerInvariant();
        if (limpio.Length == 0) return null;

        if (int.TryParse(limpio, NumberStyles.Integer, CultureInfo.InvariantCulture, out var digitos))
            return digitos is >= 1 and <= 99 ? digitos : null;

        if (Unidades.TryGetValue(limpio, out var unidad)) return unidad;
        if (Decenas.TryGetValue(limpio, out var decena)) return decena;

        // "treinta y cinco": la única forma compuesta del rango 30..99.
        var partes = limpio.Split(" y ", StringSplitOptions.TrimEntries);
        if (partes.Length == 2
            && Decenas.TryGetValue(partes[0], out var alta)
            && Unidades.TryGetValue(partes[1], out var baja)
            && baja <= 9)
            return alta + baja;

        return null;
    }
}
```

- [ ] **Step 4: Run test to verify it passes**

Run: `dotnet test PokerProOS.slnx -p:SaltearFrontend=true --filter "FullyQualifiedName~NumeroHabladoTests"`
Expected: PASS — 15 pruebas.

- [ ] **Step 5: Commit**

```bash
git add src/PokerProOS.Application/Voz/NumeroHablado.cs tests/PokerProOS.Tests/Voz/NumeroHabladoTests.cs
git commit -m "feat: los numeros de stack se entienden en digitos y en palabras"
```

---

### Task 2: `InterpretadorDeTexto` — el reemplazo de la gramática

El corazón del cambio. Convierte el texto que oyó Chrome en un `DictadoReconocido`, y **rechaza** lo que no es una orden — que es lo que la gramática SRGS no podía hacer.

**Files:**
- Create: `src/PokerProOS.Application/Voz/InterpretadorDeTexto.cs`
- Test: `tests/PokerProOS.Tests/Voz/InterpretadorDeTextoTests.cs`

**Interfaces:**
- Consumes: `IRegistroDeVocabulario` (ya existe: `PalabrasDeStack`, `Rangos`, `Palos`, `Spots`, `Situaciones`, todos `IReadOnlyList<FormasHabladas>` salvo el primero que es `IReadOnlyList<string>`); `NumeroHablado.Interpretar` de la Task 1; el record `DictadoReconocido(decimal? StackBB, string? Spot, string? Situacion, string RangoAlto, string RangoBajo, string? Palo, float Confianza, string TextoCrudo)` de `IReconocedorDeVoz.cs`.
- Produces: `public sealed class InterpretadorDeTexto(IRegistroDeVocabulario vocabulario) { public DictadoReconocido? Interpretar(string texto, float confianza); }`

**La regla que define todo:** se consumen las formas conocidas del vocabulario y, **si sobra algún token sin consumir, se rechaza la frase entera**. Por eso "cuba" no es la reina: `cu` es una forma, `cuba` no, y no queda nada que la explique. Es estricto a propósito — al dictar una orden se dicta la orden sola, y ante la duda es preferible que no conteste a que cambie de tabla sin que se lo pidan.

- [ ] **Step 1: Write the failing test**

```csharp
using PokerProOS.Application.Voz;
using PokerProOS.Infrastructure.Voz;

namespace PokerProOS.Tests.Voz;

public class InterpretadorDeTextoTests
{
    private static InterpretadorDeTexto Armar() =>
        new(RegistroDeVocabularioJson.Cargar(Rutas.Registro("vocabulario.json")));

    [Theory]
    [InlineData("reina nueve suited", "Q", "9", "s")]
    [InlineData("as rey offsuit", "A", "K", "o")]
    [InlineData("REINA NUEVE SUITED", "Q", "9", "s")]
    public void Interpreta_una_mano(string texto, string alta, string baja, string palo)
    {
        var d = Armar().Interpretar(texto, 0.9f)!;
        Assert.Equal(alta, d.RangoAlto);
        Assert.Equal(baja, d.RangoBajo);
        Assert.Equal(palo, d.Palo);
    }

    [Fact]
    public void Interpreta_stack_y_mano_juntos()
    {
        var d = Armar().Interpretar("nueve be be reina nueve suited", 0.9f)!;
        Assert.Equal(9m, d.StackBB);
        Assert.Equal("Q", d.RangoAlto);
        Assert.Equal("9", d.RangoBajo);
    }

    /// <summary>
    /// El mismo "nueve" es el número del stack y el rango: lo que los separa
    /// es la palabra de stack que va detrás del primero.
    /// </summary>
    [Fact]
    public void El_numero_de_stack_no_se_come_el_rango()
    {
        var d = Armar().Interpretar("quince be be nueve ocho suited", 0.9f)!;
        Assert.Equal(15m, d.StackBB);
        Assert.Equal("9", d.RangoAlto);
        Assert.Equal("8", d.RangoBajo);
    }

    [Theory]
    [InlineData("contra limp", "BB_VS_SB_LIMP")]
    [InlineData("mi accion", "SB_OR")]
    public void Interpreta_un_spot_sin_mano(string texto, string spot)
    {
        var d = Armar().Interpretar(texto, 0.9f)!;
        Assert.Equal(spot, d.Spot);
        Assert.Equal("", d.RangoAlto);
        Assert.Equal("", d.RangoBajo);
    }

    [Fact]
    public void Interpreta_una_situacion_sin_mano()
    {
        var d = Armar().Interpretar("defendiendo limp", 0.9f)!;
        Assert.Equal("HU_BB_VS_LIMP_FISH", d.Situacion);
    }

    [Fact]
    public void Interpreta_un_stack_solo()
    {
        var d = Armar().Interpretar("nueve be be", 0.9f)!;
        Assert.Equal(9m, d.StackBB);
        Assert.Equal("", d.RangoAlto);
    }

    /// <summary>
    /// Lo que la gramática SRGS no podía hacer: negarse. Estaba obligada a
    /// devolver la entrada más parecida, y por eso "cuba" resolvía la reina
    /// y "contra el limite de gastos" cambiaba el spot.
    /// </summary>
    [Theory]
    [InlineData("cuba")]
    [InlineData("contra el limite de gastos")]
    [InlineData("nueve de la noche")]
    [InlineData("dame un momento")]
    [InlineData("")]
    [InlineData("reina nueve suited y despues vemos")]
    public void Rechaza_lo_que_no_es_una_orden(string texto)
        => Assert.Null(Armar().Interpretar(texto, 0.9f));

    [Fact]
    public void Una_mano_sin_palo_deja_el_palo_nulo()
    {
        var d = Armar().Interpretar("as rey", 0.9f)!;
        Assert.Equal("A", d.RangoAlto);
        Assert.Equal("K", d.RangoBajo);
        Assert.Null(d.Palo);
    }

    [Fact]
    public void Conserva_el_texto_crudo_y_la_confianza()
    {
        var d = Armar().Interpretar("as rey offsuit", 0.77f)!;
        Assert.Equal("as rey offsuit", d.TextoCrudo);
        Assert.Equal(0.77f, d.Confianza);
    }
}
```

- [ ] **Step 2: Run test to verify it fails**

Run: `dotnet test PokerProOS.slnx -p:SaltearFrontend=true --filter "FullyQualifiedName~InterpretadorDeTextoTests"`
Expected: FAIL — `error CS0246: No se encuentra el tipo 'InterpretadorDeTexto'`.

- [ ] **Step 3: Write minimal implementation**

```csharp
using System.Globalization;
using System.Text;

namespace PokerProOS.Application.Voz;

/// <summary>
/// Convierte el texto que reconoció el navegador en un <see cref="DictadoReconocido"/>.
///
/// Reemplaza a la gramática SRGS, y la diferencia que importa es que ESTE
/// puede rechazar. Una gramática está obligada a elegir la entrada más
/// parecida de su lista: ante "cuba" devolvía `cu` —la reina— con confianza
/// suficiente para pasar. Acá, si sobra un token que el vocabulario no
/// explica, se descarta la frase entera.
///
/// Es estricto a propósito: una orden se dicta sola, y ante la duda es mejor
/// no contestar que cambiar de tabla sin que lo hayan pedido.
/// </summary>
public sealed class InterpretadorDeTexto(IRegistroDeVocabulario vocabulario)
{
    public DictadoReconocido? Interpretar(string texto, float confianza)
    {
        var tokens = Normalizar(texto);
        if (tokens.Count == 0) return null;

        // null marca "ya consumido". Se busca de formas largas a cortas para
        // que "contra limp" gane sobre cualquier forma de una sola palabra
        // que empiece igual.
        var libres = new List<string?>(tokens);

        var situacion = ConsumirForma(libres, vocabulario.Situaciones);
        var spot = ConsumirForma(libres, vocabulario.Spots);
        var stack = ConsumirStack(libres);
        var palo = ConsumirForma(libres, vocabulario.Palos);
        var rangos = ConsumirRangos(libres);

        // Sobró algo que el vocabulario no explica: no es una orden.
        if (libres.Any(t => t is not null)) return null;

        var hayMano = rangos.Count == 2;
        var hayContexto = situacion is not null || spot is not null || stack is not null;
        if (!hayMano && !hayContexto) return null;

        // Un rango suelto es media mano: no alcanza para consultar, y como
        // contexto no significa nada.
        if (rangos.Count == 1) return null;

        return new DictadoReconocido(
            stack, spot, situacion,
            hayMano ? rangos[0] : "",
            hayMano ? rangos[1] : "",
            hayMano ? palo : null,
            confianza,
            texto.Trim());
    }

    /// <summary>Minúsculas, sin tildes y partido en palabras.</summary>
    private static List<string> Normalizar(string texto)
    {
        var sinTildes = new string((texto ?? "")
            .Normalize(NormalizationForm.FormD)
            .Where(c => CharUnicodeInfo.GetUnicodeCategory(c) != UnicodeCategory.NonSpacingMark)
            .ToArray());

        return sinTildes.ToLowerInvariant()
            .Split([' ', ',', '.', ';', ':', '\t', '\n'], StringSplitOptions.RemoveEmptyEntries)
            .ToList();
    }

    private static List<string> Palabras(string dicho) =>
        dicho.ToLowerInvariant().Split(' ', StringSplitOptions.RemoveEmptyEntries).ToList();

    /// <summary>
    /// Busca la primera forma de la categoría que aparezca completa y la marca
    /// consumida. De más larga a más corta: si "off" y "off suit" son las dos
    /// formas del mismo palo, ganar con la corta dejaría "suit" suelto y la
    /// frase entera se rechazaría.
    /// </summary>
    private static string? ConsumirForma(List<string?> libres, IReadOnlyList<FormasHabladas> formas)
    {
        var candidatas = formas
            .SelectMany(f => f.Dichos.Select(d => (f.Clave, Palabras: Palabras(d))))
            .OrderByDescending(c => c.Palabras.Count);

        foreach (var (clave, palabras) in candidatas)
        {
            var desde = Buscar(libres, palabras);
            if (desde < 0) continue;
            for (var i = 0; i < palabras.Count; i++) libres[desde + i] = null;
            return clave;
        }
        return null;
    }

    /// <summary>Todos los rangos que queden, en el orden en que se dijeron.</summary>
    private List<string> ConsumirRangos(List<string?> libres)
    {
        var encontrados = new List<string>();
        while (ConsumirForma(libres, vocabulario.Rangos) is { } clave)
        {
            encontrados.Add(clave);
            if (encontrados.Count == 2) break;
        }
        return encontrados;
    }

    /// <summary>
    /// Un número seguido de una palabra de stack ("nueve be be"). El número
    /// solo no alcanza: en "nueve ocho suited" los dos son rangos, y es la
    /// palabra la que convierte al primero en stack.
    /// </summary>
    private decimal? ConsumirStack(List<string?> libres)
    {
        foreach (var palabraDeStack in vocabulario.PalabrasDeStack
                     .OrderByDescending(p => Palabras(p).Count))
        {
            var palabras = Palabras(palabraDeStack);
            var desde = Buscar(libres, palabras);
            if (desde <= 0) continue;

            var numero = NumeroHablado.Interpretar(libres[desde - 1] ?? "");
            if (numero is null) continue;

            libres[desde - 1] = null;
            for (var i = 0; i < palabras.Count; i++) libres[desde + i] = null;
            return numero.Value;
        }
        return null;
    }

    /// <summary>Posición donde <paramref name="palabras"/> aparece entera y libre, o -1.</summary>
    private static int Buscar(List<string?> libres, List<string> palabras)
    {
        for (var i = 0; i + palabras.Count <= libres.Count; i++)
        {
            var coincide = true;
            for (var j = 0; j < palabras.Count && coincide; j++)
                coincide = libres[i + j] == palabras[j];
            if (coincide) return i;
        }
        return -1;
    }
}
```

- [ ] **Step 4: Run test to verify it passes**

Run: `dotnet test PokerProOS.slnx -p:SaltearFrontend=true --filter "FullyQualifiedName~InterpretadorDeTextoTests"`
Expected: PASS.

Si `"mi accion"` falla porque el vocabulario no tiene esa forma para `SB_OR`, revisá `database/registro/vocabulario.json` y usá una forma que sí exista — el test documenta el vocabulario real, no al revés.

- [ ] **Step 5: Commit**

```bash
git add src/PokerProOS.Application/Voz/InterpretadorDeTexto.cs tests/PokerProOS.Tests/Voz/InterpretadorDeTextoTests.cs
git commit -m "feat: un interprete de texto que puede rechazar lo que no es una orden"
```

---

### Task 3: el endpoint `POST /api/voz/dictado`

Entra el texto de Chrome, sale el mismo evento que hoy viaja por SSE.

**Files:**
- Modify: `src/PokerProOS.Api/Controllers/VozController.cs`
- Modify: `src/PokerProOS.Api/Program.cs` (registrar `InterpretadorDeTexto`)
- Test: `tests/PokerProOS.Tests/Voz/InterpretarYResolverTests.cs` (crear)

**Interfaces:**
- Consumes: `InterpretadorDeTexto.Interpretar` (Task 2); `CopilotoDeVoz.Procesar(DictadoReconocido)` que devuelve `EventoDeCopiloto`.
- Produces: `POST /api/voz/dictado` con cuerpo `{ "texto": "...", "confianza": 0.9 }` → `200` con el `EventoDeCopiloto`, o `200` con `{ ignorado: true }` si el intérprete rechazó.

Un texto rechazado **no es un error**: es conversación que no era para la app. Devolver 400 llenaría la consola del navegador de rojo por hablar cerca del micrófono.

- [ ] **Step 1: Write the failing test**

```csharp
using PokerProOS.Application.Tablas;
using PokerProOS.Application.Voz;
using PokerProOS.Infrastructure.Tablas;
using PokerProOS.Infrastructure.Voz;

namespace PokerProOS.Tests.Voz;

/// <summary>
/// El camino entero sin audio: texto -> intérprete -> copiloto -> respuesta.
/// Es lo que el endpoint va a encadenar.
/// </summary>
public class InterpretarYResolverTests
{
    private static (InterpretadorDeTexto Interprete, CopilotoDeVoz Copiloto) Armar()
    {
        var acciones = RegistroDeAccionesJson.Cargar(Rutas.Registro("acciones.json"));
        var vocabulario = RegistroDeVocabularioJson.Cargar(Rutas.Registro("vocabulario.json"));
        var catalogo = new CargadorDeTablas(new ValidadorDeTabla(acciones), acciones)
            .CargarDirectorio(Rutas.SemillasDeTablas);
        var reconocedor = new ReconocedorFalso();
        var copiloto = new CopilotoDeVoz(
            reconocedor,
            new SintetizadorFalso { Reconocedor = reconocedor },
            new ResolverManoHandler(catalogo),
            new RedactorDeRespuesta(acciones, vocabulario),
            new MemoriaDeContexto
            {
                Situacion = "HU_SB_OR_FISH", StackBB = 7, Spot = "SB_OR",
            },
            new AnalizadorDeMemoria(catalogo),
            catalogo);
        return (new InterpretadorDeTexto(vocabulario), copiloto);
    }

    [Fact]
    public void Un_texto_dictado_resuelve_la_mano()
    {
        var (interprete, copiloto) = Armar();
        var dictado = interprete.Interpretar("as as", 0.9f);

        Assert.NotNull(dictado);
        var evento = copiloto.Procesar(dictado!);

        Assert.True(evento.Resuelta);
        Assert.Equal("AA", evento.ManoInterpretada);
    }

    [Fact]
    public void Una_frase_de_conversacion_no_llega_al_copiloto()
        => Assert.Null(Armar().Interprete.Interpretar("contra el limite de gastos", 0.9f));
}
```

- [ ] **Step 2: Run test to verify it fails**

Run: `dotnet test PokerProOS.slnx -p:SaltearFrontend=true --filter "FullyQualifiedName~InterpretarYResolverTests"`
Expected: FAIL — no compila hasta que la Task 2 esté hecha; si ya está, debería pasar y confirma el encadenado.

- [ ] **Step 3: Write minimal implementation**

En `src/PokerProOS.Api/Program.cs`, junto a los otros `AddSingleton` de voz:

```csharp
builder.Services.AddSingleton<InterpretadorDeTexto>();
```

En `src/PokerProOS.Api/Controllers/VozController.cs`, agregar el record y el endpoint, e inyectar las dos dependencias nuevas en el constructor primario (`InterpretadorDeTexto interprete, CopilotoDeVoz copiloto2` — usá el nombre `copilotoDeVoz` para no chocar con el parámetro `copiloto`, que es el `ServicioDeCopiloto`):

```csharp
public record DictadoEnviado(string Texto, float Confianza = 0.9f);

/// <summary>
/// El texto que oyó el navegador. Un texto que el intérprete rechaza no es un
/// error: es conversación que no era para la app. Devolver 400 llenaría la
/// consola de rojo por hablar cerca del micrófono.
/// </summary>
[HttpPost("dictado")]
public IActionResult Dictado([FromBody] DictadoEnviado enviado)
{
    var dictado = interprete.Interpretar(enviado.Texto ?? "", enviado.Confianza);
    if (dictado is null) return Ok(new { ignorado = true });

    return Ok(copilotoDeVoz.Procesar(dictado));
}
```

- [ ] **Step 4: Run test to verify it passes**

Run: `dotnet test PokerProOS.slnx -p:SaltearFrontend=true`
Expected: PASS — todas.

- [ ] **Step 5: Commit**

```bash
git add src/PokerProOS.Api tests/PokerProOS.Tests/Voz/InterpretarYResolverTests.cs
git commit -m "feat: el endpoint que recibe el texto oido por el navegador"
```

---

### Task 4: el navegador oye y habla

Acá se corta la dependencia de SAPI desde el lado del usuario. **No hay pruebas automáticas**: el micrófono y los permisos no se simulan. Se verifica dictando.

**Files:**
- Create: `frontend/src/tipos/speech.d.ts`
- Create: `frontend/src/core/hooks/useVozDelNavegador.ts`
- Modify: `frontend/src/core/services/tablasApi.ts` (agregar `enviarDictado`)
- Modify: `frontend/src/App.tsx:13` — ahí se llama `useEstadoDeVoz()` y se arma el objeto `voz` que baja a `PaginaDeTablas`

**Interfaces:**
- Consumes: `POST /api/voz/dictado` (Task 3).
- Produces: `useVozDelNavegador(): { disponible: boolean, activo: boolean, falla: string | null, alternar: () => void }`, con la misma forma que hoy consume `ControlDeVoz` vía `PropsDeVoz`.

- [ ] **Step 1: Agregar el envío del dictado**

En `frontend/src/core/services/tablasApi.ts`:

```typescript
/** Le manda al servidor lo que el navegador oyó. */
export async function enviarDictado(texto: string, confianza: number): Promise<void> {
  await fetch('/api/voz/dictado', {
    method: 'POST',
    headers: { 'Content-Type': 'application/json' },
    body: JSON.stringify({ texto, confianza }),
  }).catch(() => {
    // Un dictado perdido no puede romper la escucha: se sigue oyendo.
  })
}
```

- [ ] **Step 1b: Declarar los tipos de la Web Speech API**

TypeScript **no trae** `SpeechRecognition` en su librería del DOM: sin esto,
`tsc` falla. `SpeechSynthesisUtterance` sí viene incluido.

`frontend/src/tipos/speech.d.ts`:

```typescript
// La Web Speech API no está en la libreria estandar de TypeScript porque
// nunca salió de borrador, aunque Chrome la implementa desde hace años.
// Solo se declara lo que este proyecto usa.
interface SpeechRecognitionAlternative {
  readonly transcript: string
  readonly confidence: number
}

interface SpeechRecognitionResult {
  readonly isFinal: boolean
  readonly length: number
  [indice: number]: SpeechRecognitionAlternative
}

interface SpeechRecognitionResultList {
  readonly length: number
  [indice: number]: SpeechRecognitionResult
}

interface SpeechRecognitionEvent extends Event {
  readonly results: SpeechRecognitionResultList
}

interface SpeechRecognitionErrorEvent extends Event {
  readonly error: string
}

declare class SpeechRecognition extends EventTarget {
  lang: string
  continuous: boolean
  interimResults: boolean
  onresult: ((evento: SpeechRecognitionEvent) => void) | null
  onerror: ((evento: SpeechRecognitionErrorEvent) => void) | null
  onend: (() => void) | null
  start(): void
  stop(): void
}
```

Verificar que `tsconfig` incluya `src/**/*.d.ts` (lo hace si el `include` es
`src`). Comprobar con `cd frontend && npx tsc -b --noEmit`.

- [ ] **Step 2: Escribir el hook**

`frontend/src/core/hooks/useVozDelNavegador.ts`:

```typescript
import { useCallback, useEffect, useRef, useState } from 'react'
import { enviarDictado } from '../services/tablasApi'

/** La API vive con prefijo en Chrome y sin prefijo en el estándar. */
type ConstructorDeReconocimiento = new () => SpeechRecognition
const Reconocimiento: ConstructorDeReconocimiento | undefined =
  (window as unknown as { SpeechRecognition?: ConstructorDeReconocimiento }).SpeechRecognition
  ?? (window as unknown as { webkitSpeechRecognition?: ConstructorDeReconocimiento }).webkitSpeechRecognition

/**
 * El copiloto del lado del navegador: oye con la Web Speech API y habla con
 * speechSynthesis.
 *
 * Los dos van juntos por una razón concreta: mientras la app habla hay que
 * dejar de escuchar, o el micrófono toma la respuesta y dispara una consulta
 * con la propia voz de la app. Teniendo las dos puntas acá, silenciar es
 * apagar el reconocimiento durante la frase.
 */
export function useVozDelNavegador(respuesta: string | null) {
  const [activo, setActivo] = useState(false)
  const [falla, setFalla] = useState<string | null>(null)
  const motor = useRef<SpeechRecognition | null>(null)
  const hablando = useRef(false)

  const disponible = Reconocimiento !== undefined

  useEffect(() => {
    if (!disponible || !activo) return

    const r = new Reconocimiento!()
    r.lang = 'es-ES'
    r.continuous = true
    // Los parciales son una frase a medio formar: resolverlos daría
    // respuestas contra manos que todavía no se terminaron de decir.
    r.interimResults = false

    r.onresult = (evento) => {
      if (hablando.current) return
      const ultimo = evento.results[evento.results.length - 1]
      if (!ultimo.isFinal) return
      void enviarDictado(ultimo[0].transcript, ultimo[0].confidence || 0.9)
    }
    r.onerror = (evento) => {
      // "no-speech" es silencio, no una falla: Chrome lo emite todo el tiempo.
      if (evento.error !== 'no-speech') setFalla(evento.error)
    }
    // Chrome corta la escucha continua sola cada tanto; reengancharla acá es
    // el equivalente del watchdog que tenía el reconocedor de SAPI.
    r.onend = () => { if (activo && !hablando.current) try { r.start() } catch { /* ya corriendo */ } }

    motor.current = r
    try { r.start() } catch (e) { setFalla(String(e)) }

    return () => { motor.current = null; r.onend = null; r.stop() }
  }, [disponible, activo])

  // Hablar la respuesta, con el micrófono apagado mientras dura.
  useEffect(() => {
    if (!respuesta || !activo) return
    hablando.current = true
    motor.current?.stop()

    const frase = new SpeechSynthesisUtterance(respuesta)
    frase.lang = 'es-ES'
    frase.onend = () => {
      hablando.current = false
      try { motor.current?.start() } catch { /* ya corriendo */ }
    }
    window.speechSynthesis.speak(frase)
  }, [respuesta, activo])

  const alternar = useCallback(() => {
    setFalla(null)
    setActivo((previo) => !previo)
  }, [])

  return { disponible, activo, falla, alternar }
}
```

- [ ] **Step 3: Conectarlo donde hoy se arma `PropsDeVoz`**

Buscá con `grep -rn "useEstadoDeVoz" frontend/src` el lugar donde se construye el objeto `voz` que recibe `PaginaDeTablas`, y reemplazá el origen de `disponible`, `activo`, `falla` y `onAlternar` por los del hook nuevo. `respuesta` sale de `ultimo?.respuesta` (el evento SSE), así el navegador habla lo que el servidor redactó.

- [ ] **Step 4: Verificar a mano**

```bash
dotnet run --project src/PokerProOS.Api
```

Abrir Chrome en http://localhost:5000, encender la voz, aceptar el permiso de micrófono y comprobar, uno por uno:

1. Decir *"as as"* → responde la acción hablada y resalta la celda.
2. Decir *"nueve be be"* → cambia el stack.
3. Decir en voz alta una frase cualquiera de conversación → **no pasa nada** (esto es lo que antes cambiaba de spot).
4. Mientras la app habla, comprobar que no se dispara una consulta con su propia respuesta.

- [ ] **Step 5: Commit**

```bash
git add frontend/src
git commit -m "feat: el navegador oye y habla con la Web Speech API"
```

---

### Task 5: sacar SAPI y liberar al Api de Windows

Solo después de que la Task 4 funcione dictando de verdad.

**Files:**
- Modify: `src/PokerProOS.Api/PokerProOS.Api.csproj` (línea 4: `net10.0-windows` → `net10.0`; línea 20: borrar el `ProjectReference` a `PokerProOS.Voz.Sapi`)
- Modify: `PokerProOS.slnx` (borrar la línea del proyecto `PokerProOS.Voz.Sapi`)
- Modify: `src/PokerProOS.Api/Program.cs` (borrar los registros de `IReconocedorDeVoz`, `ISintetizadorDeVoz`, `GeneradorDeGramatica` y `ServicioDeCopiloto`)
- Modify: `src/PokerProOS.Api/Controllers/VozController.cs` (`estado`, `encender`, `apagar` y `capturar` dejan de hablar con el motor)
- Modify: `src/PokerProOS.Application/Voz/CopilotoDeVoz.cs` (deja de depender de `IReconocedorDeVoz` y `ISintetizadorDeVoz`)
- Delete: `src/PokerProOS.Api/Voz/ServicioDeCopiloto.cs`
- Delete: `tests/PokerProOS.Tests/Voz/ReconocedorSapiTests.cs`, `GeneradorDeGramaticaTests.cs`
- Keep, sin referenciar: `src/PokerProOS.Voz.Sapi/`

**Interfaces:**
- `CopilotoDeVoz` pasa a `CopilotoDeVoz(ResolverManoHandler resolver, RedactorDeRespuesta redactor, MemoriaDeContexto memoria, AnalizadorDeMemoria analizador, ICatalogoDeTablas catalogo)`. Pierde `Conectar()`, `Publicar` deja de pausar el reconocedor y de hablar: solo levanta el evento `Publicado`, que `CanalDeEventos` ya escucha para el SSE. Hablar ahora es del navegador.
- `EditorDeVocabularioJson` deja de recibir `IReconocedorDeVoz`: ya no hay gramática que recargar, porque el intérprete lee `VocabularioVivo` en cada llamada.

- [ ] **Step 1: Ver las pruebas fallar por la razón correcta**

Aplicá los cambios de `.csproj` y `.slnx` primero y compilá:

Run: `dotnet build PokerProOS.slnx -p:SaltearFrontend=true`
Expected: FAIL, con errores en `Program.cs` y `CopilotoDeVoz.cs` por los tipos de SAPI que ya no existen. Esa lista de errores es la guía de lo que hay que sacar.

- [ ] **Step 2: Podar `CopilotoDeVoz`**

Sacar del constructor `IReconocedorDeVoz` e `ISintetizadorDeVoz`, borrar `Conectar()`, `_conectado`, `FalloAlHablar` y `AvisarFallo`, y dejar `Publicar` en una línea: `Publicado?.Invoke(this, evento);`.

- [ ] **Step 3: Podar `Program.cs`, el controlador y borrar `ServicioDeCopiloto`**

Los endpoints `encender`/`apagar` pasan a devolver `Ok(new { activo })` sin tocar nada del servidor —el estado real vive en el navegador—, y `estado` reporta solo `ultimaFrase` del canal. `capturar` se borra: la página de vocabulario va a usar el reconocimiento del navegador.

- [ ] **Step 3b: La página de vocabulario captura con el navegador**

Al borrar `/api/voz/capturar` queda colgado
`frontend/src/features/voz/PaginaDeVocabulario.tsx:35`, que lo llama por
`capturarDictado()`. Reemplazar esa función en `tablasApi.ts` por una que
escuche una vez con la Web Speech API:

```typescript
/**
 * Escucha una vez y devuelve lo que el navegador oyó, sin interpretar. Sirve
 * para capturar cómo suena una persona diciendo algo y ofrecerlo como forma
 * nueva del vocabulario.
 */
export function capturarDictado(): Promise<string | null> {
  const Motor = (window as unknown as { SpeechRecognition?: new () => SpeechRecognition })
    .SpeechRecognition
    ?? (window as unknown as { webkitSpeechRecognition?: new () => SpeechRecognition })
      .webkitSpeechRecognition
  if (!Motor) return Promise.resolve(null)

  return new Promise((resolver) => {
    const r = new Motor()
    r.lang = 'es-ES'
    r.continuous = false
    r.interimResults = false
    r.onresult = (evento) => resolver(evento.results[0][0].transcript)
    r.onerror = () => resolver(null)
    r.onend = () => resolver(null)
    r.start()
  })
}
```

`PaginaDeVocabulario.tsx` no cambia: sigue llamando `capturarDictado()` y
recibiendo `string | null`. **Esta es la parte que más mejora del cambio**:
capturar cómo suena alguien diciendo "dama" era imposible con un motor que oye
"Gana".

- [ ] **Step 4: Correr todo**

Run: `dotnet test PokerProOS.slnx -p:SaltearFrontend=true`
Expected: PASS. Las pruebas de SAPI ya no están; las de `CopilotoDeVozTests` hay que actualizarlas al constructor nuevo (sin reconocedor ni sintetizador) y borrar las que verificaban el pausado y el fallo al hablar, que ahora son del navegador.

Verificá además que el target quedó bien:

Run: `grep TargetFramework src/PokerProOS.Api/PokerProOS.Api.csproj`
Expected: `<TargetFramework>net10.0</TargetFramework>`

- [ ] **Step 5: Commit**

```bash
git add -A
git commit -m "refactor: se va SAPI y el Api deja de necesitar Windows"
```

---

## Verificación final

- [ ] `dotnet test PokerProOS.slnx -p:SaltearFrontend=true` en verde.
- [ ] `cd frontend && npx tsc -b --noEmit && npx oxlint` sin errores.
- [ ] La app levanta y `/api/tablas` devuelve `problemas: 0`.
- [ ] Dictando en Chrome: una mano resuelve, una orden de contexto cambia de tabla, y una frase de conversación no hace nada.
