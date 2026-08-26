# PokerProOS

Una herramienta de estudio de preflop para Spin & Go. Muestra las tablas de estrategia heads-up
(169 manos iniciales, coloreadas por acción) y las responde en voz alta: un copiloto escucha por
micrófono, entiende la mano que dictás y contesta hablado mientras resalta la celda en pantalla, sin
que tengas que soltar las cartas para mirar la tabla.

## Cómo correrlo

```bash
dotnet run --project src/PokerProOS.Api
```

Abre `http://localhost:5000`. El build del backend ya incluye el frontend compilado — no hay que correr
Vite aparte para usar la app. Si venís de cambiar algo en `frontend/`, `dotnet build` lo reconstruye solo.

## Qué necesita

- **Windows**, con el reconocedor de voz **es-ES** instalado (el que trae Windows para español). El
  copiloto usa Windows SAPI (`System.Speech`), que no existe fuera de Windows.
- **SQL Server es opcional.** Si no está corriendo, la aplicación arranca igual, las tablas y la voz
  funcionan igual — solo se pierde el historial de consultas dictadas.
- .NET 10 SDK y Node.js para compilar (Node solo hace falta si tocás el frontend; el build del backend
  lo invoca automáticamente).

## Cómo se dicta una consulta

Con la app corriendo y el micrófono activo, alcanza con hablar la mano — el stack y el spot activos se
mantienen entre consultas hasta que dictás uno nuevo:

- **«siete be be, a rey offsuit»** — fija el stack en 7bb y pregunta por AKo. Responde la acción
  (por ejemplo «CALL.») y resalta la celda AKo en la grilla.
- **«a rey»** — sin repetir el stack, consulta AKs o AKo según lo que se haya dicho de palo; si no se
  especifica palo se asume offsuit, y como se asumió, la respuesta repite la mano: «A K offsuit: CALL.»
- **«diez be be, reina reina, contra all in»** — cambia de stack (10bb) y de spot (versus all-in) en la
  misma frase, y consulta QQ.

Si el micrófono no está disponible, la app sigue sirviendo las tablas igual — simplemente arranca sin voz,
y lo indica en pantalla.
