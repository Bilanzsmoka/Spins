# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## What this is

PokerProOS — a Spin & Go poker study tool. A .NET 10 Web API (Clean Architecture, EF Core + SQL Server) serves both a JSON API and a compiled React 19 SPA from `wwwroot`. The core domain is preflop strategy charts: a 13×13 grid of the 169 starting hands, each mapped to an action, per situation / stack size / spot.

## Commands

Backend (from repo root):

```bash
dotnet build PokerProOS.slnx            # .slnx is the active solution; PokerProOS.sln is the legacy copy
dotnet run --project src/PokerProOS.Api # http://localhost:5000, Swagger UI at /swagger
```

Frontend (from `frontend/`):

```bash
npm install
npm run dev      # Vite dev server — see the wwwroot note below
npm run build    # tsc -b && vite build → frontend/dist
npm run lint     # oxlint (not eslint)
```

There is no test project and no test runner configured — `dotnet test` finds nothing.

### Serving the frontend

`vite.config.ts` has **no dev proxy**, and `chartApi.ts` fetches relative `/api/...`. So `npm run dev` alone cannot reach the backend. The working loop is: `npm run build`, then copy `frontend/dist/*` into `src/PokerProOS.Api/wwwroot/`, then run the API — `Program.cs` does `UseStaticFiles` + `MapFallbackToFile("index.html")`. The copy is manual; there is no build target wiring it up.

## Architecture

Dependency direction is strict: `Domain ← Application ← Infrastructure ← Api`. Application declares repository interfaces (`IChartRepository`, `ISessionRepository`, `ITrainerRepository`); Infrastructure implements them over `PokerProOSDbContext`. Handlers (`GetChartByStackHandler`, `EvaluateAnswerHandler`, `CreateSessionHandler`) are plain classes wired by hand in `Program.cs` — there is no MediatR, so a new handler must be registered in `Program.cs` explicitly.

Application code is organized by feature slice (`Charts/`, `Sessions/`, `Trainer/`), each with `Commands/`, `Queries/`, `DTOs/`, `Interfaces/`. The React side mirrors this: `frontend/src/core/` holds cross-cutting models, services, hooks, constants; `frontend/src/features/spins/charts/` holds the chart UI.

### Database and seeding

No EF migrations exist. `Program.cs` calls `EnsureCreatedAsync()` at startup and then runs `ChartImportService.ImportFromDirectoryAsync` against `database/seed-data`. Because `EnsureCreated` never evolves an existing schema, **any entity or `IEntityTypeConfiguration` change requires dropping the `PokerProOS` database** (or introducing migrations) before it takes effect.

The seed path is resolved as five directories up from `AppContext.BaseDirectory` — it only resolves when running from the source tree's `bin/Debug/net10.0`. A published build will silently skip seeding.

Import is idempotent by design: for each `(SituationKey, StackKey)` group it calls `DeleteByStackAsync` before inserting. `ChartStrategyCell` has a unique index on `(SituationKey, StackKey, SpotKey, HandLabel)`.

### Chart JSON format and the REST sentinel

Seed files (`database/seed-data/hu-sb-or-fish-*.json`) are shaped `situation → stacks[] → spots[] → actions{}`. Inside `actions`, a key maps either to an array of hand labels **or** to the literal string `"REST"`. `ChartImportService` assigns the listed hands first, then fills every one of the 169 generated hands not otherwise assigned with the `REST` action. Exactly one action per spot should be `"REST"`; the file's `expectedCounts` and `checks` blocks are documentation only — nothing reads them (`ChartValidator` is a stub that always returns valid).

### String keys, not enums

`Domain/Enums/` (`Actions`, `Situation`, `Spot`) exists but is **unused** — every entity, DTO, and query passes raw strings. The enum spelling and the data spelling differ (`Actions.ALL_IN` vs. the JSON/DB value `"ALL-IN"`), so do not treat the enums as the source of truth. The live vocabulary is: situation `HU_SB_OR_FISH`; actions `ALL-IN`, `CALL`, `FOLD`, `RAISE_X2`; stack keys as listed in `AVAILABLE_STACKS` (`frontend/src/core/constants/poker.ts`), which include ranges like `1-4bb` and `11-12bb`.

Related gotcha: `EvaluateAnswerHandler` builds its stack key as `$"{query.StackBB}bb"` from an `int`, so it can only ever match the single-value stacks (`5bb`…`10bb`) and never the ranged ones.

### Hand-label conventions

Two independent generators must stay in agreement: `ChartImportService.GenerateAllHands()` (C#) and `ChartGrid.getHandLabel()` (TSX). Both use rank order `A K Q J T 9 8 7 6 5 4 3 2`, pairs on the diagonal (`AA`), suited above it (`AKs`), offsuit below (`AKo`) — higher rank always first. `HandLabel` in Domain validates this shape but is not currently called from the import path.

## Leftovers

`WeatherForecast.cs` and `WeatherForecastController.cs` are unremoved `dotnet new webapi` scaffolding.
