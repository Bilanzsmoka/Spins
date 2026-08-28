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
dotnet test PokerProOS.slnx             # 114 tests; the voice-recognition ones drive real audio and are slow
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
hand-edit, and colors, table validation and the voice grammar all depend on them — if either is missing
or has a syntax error, there is nothing useful to serve, so the app prints the cause to stderr and exits
with a non-zero code before any host or logger exists (`RegistroInvalidoException`, caught in
`Program.cs`'s `CargarRegistroOTerminar`).

With the registries loaded, `CargadorDeTablas` (using `ValidadorDeTabla`) reads every `*.json` file in
`database/seed-data/` and builds a `CatalogoEnMemoria` — this in-memory object, not the database, is what
`TablasController` serves over `/api/tablas`, and what `InterpretadorDeTexto` resolves dictated text
against. Data paths are resolved from `AppContext.BaseDirectory` because the `.csproj` copies
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
list, so a new chart or a new vocabulary entry changes what can be said without a code change. Text it
does not recognize is not an error: it is conversation that was not meant for the app, and the endpoint
answers `{ ignorado: true }` instead of a 400 that would paint the console red for talking near the
microphone.

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

## Agregar una tabla nueva

1. Dejar el archivo JSON en `database/seed-data/`.
2. Si usa una acción que no existe, agregarla a `database/registro/acciones.json`.
3. Arrancar. La app valida, carga y sincroniza sola; el intérprete de voz lee el catálogo nuevo sin
   tocar nada.

No hay que tocar código. Si algo falla, la app lo dice al arrancar y en pantalla, indicando archivo,
stack, spot y causa.
