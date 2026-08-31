# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## What this is

PokerProOS — a Spin & Go poker study tool. A .NET 10 Web API (Clean Architecture, EF Core + SQL Server)
serves both a JSON API and a compiled React 19 SPA from `wwwroot`. The core domain is preflop strategy
charts: a 13×13 grid of the 169 starting hands, each mapped to an action, per situation / stack size /
spot. The browser listens continuously through the Web Speech API and posts what it heard to the API,
which interprets the text and answers — "siete be be, a rey offsuit" gets a spoken action and a
highlighted cell, hands-free, while studying away from the keyboard.

## Commands

Backend (from repo root):

```bash
dotnet build PokerProOS.slnx            # .slnx is the active solution; PokerProOS.slnx es la única solución. is the 
dotnet run --project src/PokerProOS.Api # http://localhost:5000, Swagger UI at /swagger (Development only)
dotnet test PokerProOS.slnx -p:SaltearFrontend=true   # 211 tests, in-process and fast — nothing drives real audio
```

`dotnet build` now builds and copies the frontend for you — see below. There is no manual step left.

Frontend (only needed when iterating on the UI directly with hot reload; from `frontend/`):

```bash
npm install
npm run dev      # Vite dev server; vite.config.ts proxies /api to http://localhost:5000
npm run build    # tsc -b && vite build → frontend/dist
npm run lint     # oxlint (not eslint)
```

### The frontend build is part of `dotnet build`

`src/PokerProOS.Api/PokerProOS.Api.csproj` has an MSBuild target named `ConstruirFrontend` that runs
before `Build`: it `npm install`s if `node_modules` is missing, runs
`npm run build`, wipes `wwwroot`, and copies `frontend/dist` into it. Building the API always ships the
current frontend — there is no way to end up running a stale UI by accident.

When you're only touching C# and want to skip paying for the Vite build on every iteration:

```bash
dotnet build -p:SaltearFrontend=true
```

This leaves whatever is already in `wwwroot` untouched (or leaves it absent, if it never existed).

## Architecture

Dependency direction is strict: `Domain ← Application ← Infrastructure ← Api`. Every project targets
plain `net10.0`: no server-side audio means no Windows requirement. A fifth project,
`PokerProOS.Voz.Sapi`, still sits in the repository with the old `System.Speech` recognizer and
synthesizer, but it is **not in `PokerProOS.slnx` and nothing references it** — it is kept on purpose,
in case the browser disappoints in real use. That is also why `IReconocedorDeVoz` and
`ISintetizadorDeVoz` remain in `Application/Voz/` with nothing implementing them: they are the contract
that project needs to compile again. There is no MediatR — handlers (`ResolverManoHandler`,
`CopilotoDeVoz`, …) are plain classes registered by hand in `Program.cs`.

Application code is organized by feature slice: `Tablas/` (chart resolution — `ICatalogoDeTablas`,
`IRegistroDeAcciones`, `ResolverManoHandler`), `Voz/` (the voice pipeline — `CopilotoDeVoz`,
`MemoriaDeContexto`, `RedactorDeRespuesta`, `InterpretadorDeTexto`), `Bitacora/` (`IBitacoraDeConsultas`, the query-history port). Infrastructure
mirrors this with `Tablas/` (JSON loading and validation), `Voz/` (the vocabulary registry), and
`Database/` (EF Core). The React side mirrors it too: `frontend/src/core/` holds cross-cutting hooks,
models and API services; `frontend/src/features/tablas/` holds the grid, cell, legend, selector and
voice-status components — all named in Spanish, matching the backend.

### JSON is the source of truth; the in-memory catalogue is the read path

At startup `Program.cs` loads `database/registro/acciones.json` and `database/registro/vocabulario.json`
(`RegistroDeAccionesJson`, `RegistroDeVocabularioJson`). These are the only files a user is expected to
hand-edit, and colors, table validation and the voice interpreter all depend on them — if either is missing
or has a syntax error, there is nothing useful to serve, so the app prints the cause to stderr and exits
with a non-zero code before any host or logger exists (`RegistroInvalidoException`, caught in
`Program.cs`'s `CargarRegistroOTerminar`).

With the registries loaded, `CargadorDeTablas` (using `ValidadorDeTabla`) reads every `*.json` file in
`database/seed-data/` and builds a `CatalogoEnMemoria` — this in-memory object, not the database, is what
`TablasController` serves over `/api/tablas`, and what `ResolverManoHandler` answers an already
interpreted hand against. The interpreter never reads the catalogue: it only reads the
vocabulary registry. Data paths are resolved from `AppContext.BaseDirectory` because the `.csproj` copies
`database/**/*.json` into the build output (`Content Include="..\..\database\**\*.json"`) — no walking up
a fixed number of parent directories.

Validation is per-file and does not stop at the first bad table: `ValidadorDeTabla` checks each file's
JSON validity, that every declared action exists in the action registry, that every hand belongs to the
169-hand matrix, that no hand is assigned twice, that exactly one action per spot is marked `"REST"`,
that all 169 hands end up covered, and — when the file declares them — that its own `expectedCounts` and
`checks` blocks agree with what the file actually resolves to. A file that fails becomes a
`ProblemaDeTabla` (file, stack, spot, message) instead of crashing the whole catalogue; the other files
still load. `catalogo.Problemas` is served in `/api/tablas`'s response and rendered by
`AvisoDeProblemas.tsx`, so a broken table is visible both at startup (a logged warning) and on screen —
naming the file, the stack/spot it's in, and the reason.

### Database: EF migrations mirror the catalogue; the app runs fine without SQL Server

EF migrations exist under `src/PokerProOS.Infrastructure/Database/Migraciones/`. At startup `Program.cs`
calls `Database.MigrateAsync()` and then `SincronizadorDeCatalogo.SincronizarAsync` copies the in-memory
catalogue into the `ChartStrategyCell` table (a full delete-then-insert — the JSON stays authoritative,
the table is a relational mirror for cross-cutting queries, not a second source of truth). Every voice
consultation is also logged to `ConsultaDeVoz` via `IBitacoraDeConsultas`. Both are wrapped in
try/catch: if SQL Server is unreachable, the app logs a warning and keeps serving charts and voice
queries with no history — nothing about studying a chart depends on the database being up. The
`DbContext` still exposes three tables (`SpinSessions`, `SpinTournaments`, `TrainerAttempts`) left over
from an earlier iteration of the project; no handler reads or writes them today.

### Chart JSON format

Seed files (`database/seed-data/hu-sb-or-fish-*.json`) are shaped `situation → stacks[] → spots[] →
actions{}`. Inside `actions`, a key maps either to an array of hand labels **or** to the literal string
`"REST"` — the validator enforces exactly one `REST` per spot, and `CargadorDeTablas` assigns the listed
hands first, then fills every one of the 169 generated hands not otherwise assigned with the `REST`
action. A file may optionally include `expectedCounts` and `checks` blocks as self-documentation; if
present, the validator checks them against what the file actually resolves to and reports a problem if
they disagree — they are not decorative.

Un spot puede además declarar `"tip"`: una frase escrita a mano con el porqué
estratégico de esa tabla. Es opcional, se edita desde el popup de la ficha (que
escribe el JSON vía `IEditorDeTablas`, igual que la corrección de celdas), y el
validador sólo se queja si la clave existe pero está vacía.

### Hand-label conventions

Two independent generators must stay in agreement: `MatrizDeManos` (C#, in `PokerProOS.Domain.Manos`) and
`Grilla.tsx`'s hand-label logic (TSX). Both use rank order `A K Q J T 9 8 7 6 5 4 3 2`, pairs on the
diagonal (`AA`), suited above it (`AKs`), offsuit below (`AKo`) — higher rank always first. `A K Q J T 9
8 7 6 5 4 3 2` (the 13 ranks) and `169` (the size of the resulting matrix) are the only bare constants
the project allows; everything else — actions, colors, stacks, spots, spoken vocabulary — comes from
`database/registro/` or `database/seed-data/`, never from code.

### The voice loop

The microphone belongs to the browser. `useVozDelNavegador` (frontend) listens continuously with the
Web Speech API and POSTs each final transcript to `/api/voz/dictado`. `InterpretadorDeTexto`
(Application) turns that free text into a `DictadoReconocido` by matching it against the vocabulary
registry — the set of understandable stacks, spots, situations and hand ranks is never a hardcoded
list, so adding a `dicho` to `database/registro/vocabulario.json` changes what can be said without a
code change. Note the limit: the interpreter reads **only** the vocabulary registry, never the
catalogue. A new chart does not teach it any new word — if that chart brings a spot or a situation
nobody can name yet, it has to be added to `vocabulario.json` (by hand or from the Voz screen) or it
cannot be dictated. Text it does not recognize is not an error: it is conversation that was not meant for the app, and the endpoint
answers `{ ignorado: true }` instead of a 400 that would paint the console red for talking near the
microphone.

### Dictado dirigido: nombrar el nivel

`InterpretadorDeTexto` barre todas las categorías en una sola pasada, de la forma más larga a la más
corta, sin noción de posición ni de flujo. Sobre el vocabulario real eso da **121 choques entre
categorías**: `"tres max"` (el formato) se come el `"tres"` que era el rango, `"be be contra limp"`
(la situación) se come el `"contra limp"` que era el spot y el `"be be"` que era el stack. No es
teórico — es lo que hace que una carta termine cambiando el spot.

Por eso una consulta se puede dictar **dirigida**: encabezándola con la palabra del nivel —`"spot
contra limp"`, `"stack doce"`, `"mano as rey"`— el intérprete busca **solo** en esa categoría y no
hay dos compitiendo por las mismas palabras. Las palabras salen de la sección `niveles` de
`vocabulario.json` (claves de `NivelDeDictado`: Formato, Situacion, Stack, Spot, Mano) y se editan
como cualquier otra forma.

Tres reglas que la hacen predecible:

- **La etiqueta cuenta solo en la posición 0.** Si contara en cualquier lugar, mencionarla al pasar
  cambiaría el modo de interpretación sin que nadie lo pidiera.
- **Dicho el nivel, lo que no pertenece a ese nivel se rechaza** en vez de adivinarse: `"spot as
  rey"` no resuelve. Es todo el punto.
- **Pero la etiqueta dirige solo si lo que sigue resuelve ahí.** `"mano"` encabeza un dictado y
  además arranca `"mano a mano"`, el formato heads-up; si el camino dirigido no da nada, la frase
  entera vuelve al barrido libre intacta. Sin esto, agregar una etiqueta rompe formas que ya
  funcionaban.

Con el nivel dicho, un stack no necesita la palabra detrás: `"stack doce"` alcanza, porque lo que
distinguía un número de un rango era justamente ese `"be be"`.

El dictado libre de siempre sigue funcionando igual. La etiqueta es un atajo para cuando confunde,
no una obligación.

### El vocabulario se enseña mientras estudiás

Chrome no conoce la jerga: "be be contra min raise" le sale "vivir versus race", y adivinar más
variantes no escala porque dependen de cómo habla cada uno. Por eso una frase rechazada no se
pierde — `CopilotoDeVoz.NoEntendido` la publica igual, el navegador dice "No te entendí" y
`FrasesSinEntender.tsx` la deja en pantalla con las listas para decirle qué era. Se guarda por
`POST /api/voz/vocabulario/{categoria}/{clave}`, y como el vocabulario es vivo (`VocabularioVivo`)
entra en el dictado siguiente sin reiniciar. Juntar las fallas en vez de preguntar en el momento es
lo que permite dictar sin manos: seguís estudiando y se las enseñás al volver al teclado.

Enseñar un **rango** es lo que generaliza — una forma nueva de "nueve" arregla todas las manos que
lleven un nueve. La categoría `manos` es la excepción: mapea una frase entera a una clave de la
matriz ("AKo"), y existe para cuando el navegador funde las dos cartas en algo que no se puede
partir. Es la única categoría cuyas claves no están listadas de antemano —son las 169— así que
`EditorDeVocabularioJson` valida contra `MatrizDeManos`, crea la entrada al guardar la primera
forma y la borra al quedarse sin ninguna. La sección `manos` de `vocabulario.json` es opcional y
normalmente no existe.

`CopilotoDeVoz` (Application) does the rest: it updates `MemoriaDeContexto` (so a dictated stack or
spot persists until the next one overrides it — no need to repeat "seven bb" every time), resolves the
hand against the catalogue via `ResolverManoHandler`, composes the reply with `RedactorDeRespuesta` and
raises `Publicado` with an `EventoDeCopiloto`. `Program.cs` hooks that event — once, right after
`builder.Build()` — to `CanalDeEventos` (an SSE stream the frontend consumes at `/api/voz/eventos` to
highlight the resolved cell and color the response text with that action's color) and to the query log
in the background. The browser speaks what arrives over SSE with `speechSynthesis`, and stops listening
while it talks so it does not hear itself. If the microphone is not available, the app still serves the
charts — it just runs without a voice.

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

### El entrenador

La unidad de repetición es la **casilla** —situación, stack, spot y mano
juntos, no la mano sola—: la misma mano en dos spots distintos son dos
casillas con su propio calendario, porque saberla en una no dice nada de la
otra. `CalendarioDeRepeticion` la mueve por una escalera de intervalos fija,
`[1, 3, 7, 16, 35, 90]` días: cada acierto sube un escalón, el último se
repite para siempre, y fallar no baja un escalón sino que vuelve a cero y
vence hoy mismo, para que la casilla reentre en la tanda actual. Cuando
`PlanificadorDeTanda` necesita rellenar con material nuevo (sin progreso
previo), prioriza los **bordes** —donde se corta el bloque de una familia o
cambia el umbral de stack— porque son las casillas que separan saber la
tabla de adivinarla; el resto entra después, para que la tanda igual se
llene cuando los bordes se agotan. Quién entrena se resuelve en un único
lugar, `EntrenadorController.UsuarioActual`, para que agregar login sea
cambiar de dónde sale ese número y nada más. Y es lo único de la app que
**no** anda sin SQL Server: sin base no hay dónde guardar el calendario, así
que a diferencia de las tablas y la voz, un error de base acá se muestra en
pantalla en vez de tragarse.

**Los errores pesan distinto.** `acciones.json` declara una `agresion` por
acción —FOLD 0, CHECK 2, CALL 3, las subidas de 4 a 14, ALL-IN 15— y con eso
`ResponderRespuestaHandler` mide qué tan lejos quedó la respuesta. Una acción
vecina es un desliz de tamaño: la casilla baja **un** escalón. Cualquier otra
cosa vuelve a cero. Las dos reentran hoy. FOLD queda separado de CHECK a
propósito: confundir dos tamaños de subida es un desliz, tirar donde se podía
pasar gratis no. La escala es dato, no código — deducir que RAISE_X4 pesa más
que RAISE_X3 leyendo el número de la clave es lo que este proyecto no hace.

Y la bitácora ya se lee: **el mapa de errores** (`GET /api/entrenador/errores`)
agrupa por casilla *y acción elegida*, y muestra sólo lo que se repitió más de
una vez. No es la lista de lo que no sabés —eso ya aparece solo en la tanda—:
es la de lo que sabés **al revés**, que es lo único que repetir no arregla.
Nota para quien lo toque: el proveedor en memoria de las pruebas no traduce a
SQL, así que un LINQ que SQL Server no sepa ejecutar pasa verde y revienta en
runtime. Pasó con esta consulta; se verifica pegándole al endpoint.

El entrenador dibuja **la mesa**, no una ficha de estudio: quién está sentado
dónde, de qué tipo es cada rival —con el color y la figura del glosario— y qué
hizo antes de tu turno, más tu stack en BB, las ciegas y un reloj que cuenta
hacia arriba y avisa con color sin reprobar nada. Y **no dice la mano**: en una
mesa nunca ves «AKo», ves dos cartas y las tenés que leer; mostrar la etiqueta
convertía el ejercicio en reconocer un código, que es lo que no se transfiere
al juego.

Todo eso sale del bloque `mesa` de cada situación —`heroe`, `ciegaChica`,
`ciegaGrande` y `rivales` con `posicion`, `tipo` y `hizo`—, **declarado, nunca
deducido de la clave**. Acá esa regla importa más que en ningún lado: una mesa
mal dibujada no rompe nada visible y enseña una mano equivocada durante meses.
Por eso las pruebas controlan el archivo y no el código que lo lee: que toda
situación declare mesa, que nadie comparta silla, que el tipo de cada rival
exista en el glosario y que las sillas coincidan con el formato.

La tanda además **se intercala**: ninguna página —situación + stack + spot—
aporta más de la mitad, el material entra de a uno por página por vuelta, y un
reparto final evita que dos seguidas compartan página. Practicar agrupado hace
rendir mejor durante la tanda y peor a la semana siguiente: con el bloque
delante, la respuesta sale de la pregunta anterior y no de la memoria. Medido
sobre las tablas reales, una tanda de diez pasó de **9 de 9 seguidas de la
misma página a 0**. Lo que el tope deja afuera no se pierde: sigue vencido y
entra en la próxima. Y por debajo de `RachaTolerable` (3) no se aprieta nada —
en una tanda chica, desplazar algo vencido para meter material nuevo es peor
negocio que la racha. El tamaño por defecto bajó de 20 a **10**: las ráfagas
cortas y frecuentes rinden más, y además se hacen.

Cada respuesta, además, queda registrada en `RespuestaRegistrada`: qué casilla
era, qué contestaste, qué decía la tabla y **cuánto tardaste**. Va aparte de
`ProgresoDeCasilla` a propósito — aquél guarda el estado y se pisa en cada
respuesta; éste guarda el hecho y no se pisa nunca. Sin historial no se puede
saber *qué* se erra repetido ni si se está contestando más rápido que el mes
pasado, y ésas son las dos preguntas que separan saber una tabla de tenerla
como reflejo. Hoy sólo se escribe: nada lo lee todavía, porque ninguna de esas
respuestas se puede evaluar sin meses de datos. El tiempo llega del navegador
—se mide desde que la pregunta aparece en pantalla, no desde que se pidió la
tanda— y se guarda cero cuando no se pudo medir: contarlo como rápido sería
peor que no contarlo.

### El plan de estudio

La app medía todo y no comparaba nada: el hábito `VOLUMEN` guardaba cuántos
torneos jugaste y ningún lado decía que la meta eran 140. El plan
(`database/registro/plan.json`) pone los objetivos, y `MedidorDeHitos` contesta
la única pregunta diaria: **¿hoy voy bien?**

Un **hito** es un objetivo con nombre, un número y una barra; uno activo a la
vez. Hay dos tipos: `saber` apunta a una situación del catálogo y lo mide el
entrenador —el porcentaje de sus **casillas de borde** que llegaron a un
descanso de `escalonMinimo` días o más—, y `jugar` apunta a un hábito numérico
y se cumple si en la ventana **nunca hubo dos días seguidos** por debajo del
objetivo.

Dos decisiones que se midieron antes de tomarlas, y sin las cuales el módulo no
sirve:

- **El denominador son los bordes, no las 169 casillas.** Las tablas tienen
  57.291 casillas y 13.210 bordes. Un hito por formato serían más de 7.000
  casillas —un año a 20 por día—; **por situación** son 357 a 2.741, o sea 2 a
  10 semanas. Por eso los hitos son 17, uno por tabla, del más chico al más
  grande. El borde es donde se corta el bloque; el interior se sabe sabiendo
  dónde termina.
- **No se muestran rachas.** Medir días seguidos hace que la gente abandone el
  hábito entero después del primer fallo; la regla que se sostiene es no fallar
  dos veces seguidas, y es la que usa el panel.

`MedidorDeHitos` es puro —recibe catálogo, progreso y marcas ya cargados, y el
día entra como parámetro—, así que se prueba entero sin base. El panel vive
arriba de **Hábitos**, y su botón *Entrenar* abre el entrenador ya filtrado en
la tabla del hito: es lo que convierte el plan en algo que se hace y no que se
mira. Como el entrenador, **no anda sin SQL Server**: sin base no hay progreso
que medir, y eso se dice en pantalla en vez de mostrar un 0% que sería mentira.

## Agregar una tabla nueva

1. Dejar el archivo JSON en `database/seed-data/`.
2. Si usa una acción que no existe, agregarla a `database/registro/acciones.json`.
3. Si trae un spot o una situación con un nombre que todavía no se puede decir, agregarle sus
   `dichos` a `database/registro/vocabulario.json` — a mano o desde la pantalla de Voz.
   `InterpretadorDeTexto` no lee el catálogo, solo el vocabulario: una tabla nueva por sí sola no
   enseña ninguna palabra nueva, y ese spot no se va a poder nombrar.
4. Arrancar. La app valida, carga y sincroniza sola.

No hay que tocar código. Si algo falla, la app lo dice al arrancar y en pantalla, indicando archivo,
stack, spot y causa.
