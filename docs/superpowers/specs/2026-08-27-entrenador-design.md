# Entrenador de tablas

Fecha: 2026-08-27
Rama: `tablas-y-copiloto-de-voz`

## Problema

La app sabe responder una mano y sabe explicarla, pero no sabe **preguntarla**.
Hoy el estudio depende de que el usuario se acuerde de mirar las tablas, y el
único recorrido que existe (`RepasoDeTablas`) muestra las grillas una tras otra
sin pedir nada: es un calentamiento, no una evaluación. Mirar una tabla y creer
que se sabe es exactamente el error que la repetición evita.

El entrenador de PokerHero, que el usuario ya usa, cubre el bucle básico —mesa
simulada, respuesta con teclado, porcentaje de acierto— y ahí se termina:

- **El error no enseña.** Dice "incorrecto" y pasa a la mano siguiente. El
  momento en que más entra una explicación es el único en que no la da.
- **No insiste.** Las manos salen al azar dentro del filtro, así que una mano
  fallada puede no volver a aparecer en toda la sesión.
- **No sabe qué no sabés.** Cada consulta de voz que hace el usuario mientras
  juega es, por definición, una mano que no tenía. Esa señal existe en la
  bitácora y no la usa nadie.

## Alcance

El entrenador se diseña **multiusuario desde el modelo de datos**: el usuario es
parte de la clave del progreso desde el primer día. Pero no se construye login ni
registro: por ahora la API resuelve el usuario con una constante (`UsuarioId = 1`)
en un solo lugar, para que agregar identidad sea cambiar de dónde sale ese número
y nada más. El día que se agregue login, el progreso ya está separado y no hay que
migrar datos.

Queda explícitamente afuera, y hay que saberlo antes de crecer:

- **Las tablas son archivos compartidos del repo.** Corregir una celda escribe el
  JSON para todos. Con varios usuarios hay que decidir si las tablas son de cada
  uno o comunes.
- **El vocabulario también es compartido.** Las formas habladas que enseña un
  usuario cambian lo que entiende la app para todos. Es el mismo problema que
  las tablas y se decide junto con ellas.

> **Nota de 2026-08-28.** Cuando se escribió este spec, la voz era un bloqueo
> multiusuario: `ServicioDeCopiloto` escuchaba por el micrófono *del servidor*
> con Windows SAPI, y con varias personas eso no existe. Eso ya se resolvió —el
> reconocimiento vive en el navegador y el servidor solo recibe texto—, así que
> ese punto salió de la lista. Lo que queda arriba es lo que sigue abierto.

## Qué se construye

### La unidad de repetición

Lo que se aprende no es una mano: es **una casilla**, o sea
`usuario + situación + stack + spot + mano`.

`K2s` en BB vs open shove a 11bb es una cosa distinta de `K2s` en el mismo spot a
20bb, porque la respuesta correcta cambia entre las dos. Aprender la tabla *es*
aprender dónde está ese corte, así que la unidad tiene que distinguirlas.

### El calendario

De cada casilla se guardan tres números: **aciertos seguidos**, **intervalo
actual** y **cuándo vence**.

| resultado | efecto |
|---|---|
| acierto | el intervalo sube un escalón: 1 → 3 → 7 → 16 → 35 → 90 días |
| fallo | el intervalo vuelve a 1, vence hoy, y la casilla reentra en la tanda actual |

Una casilla sin registro es material nuevo. La tanda se arma con **lo vencido
primero** y, si sobra lugar, se completa con material nuevo.

### El filtro y el tamaño de la tanda

Antes de arrancar se elige sobre qué entrenar. El filtro se arma **con lo que el
catálogo declara**, no con listas en código:

- **formato** (`HU` / `3-max`), que ya declara cada archivo
- **situación**, opcional; sin elegir, entran todas las del formato
- **rango de stack**, en BB, contra la cobertura real de cada tabla
- **spot**, opcional

El tamaño por defecto es **20 manos**, elegible. Si lo vencido supera el tamaño,
entra lo más vencido primero y el resto queda para la próxima; si no alcanza, se
completa con material nuevo.

### De dónde sale el material nuevo

Hay 339 spots por 169 manos: más de **57.000 casillas**. Al azar no se cubren
nunca, y la mayoría no enseña nada (`72o` foldea en casi todos lados).

El material nuevo prioriza **las casillas de borde**: las que `AnalizadorDeMemoria`
ya identifica como el punto donde se corta el bloque de una familia, o donde
cambia el umbral de stack. Son las que separan saber la tabla de adivinarla, y el
cálculo ya existe.

### La pantalla

Una mesa simulada: las dos cartas con su palo, los stacks de cada jugador, el
bote, el rival etiquetado `fish` o `reg` según lo declara la tabla, y los botones
de acción.

Los botones **salen del spot**, no de una lista en código, y llevan **el color del
registro de acciones**: si en la grilla `ALL-IN` es verde, el botón es verde. Es
la misma memoria visual que el usuario ya entrenó mirando las tablas, y romperla
sería entrenar dos cosas distintas. Los atajos de teclado salen del `orden` del
registro.

### El veredicto

Al acertar, sigue. Al fallar, **muestra la ficha de memoria completa** —el bloque
de la familia, el umbral de stack, las familias emparentadas, el peso en combos y
el tip escrito a mano— que `AnalizadorDeMemoria` ya calcula para el popup de la
grilla. No se construye lógica nueva: se reusa en el momento en que más sirve.

### La voz

El navegador canta la mano y el spot con `speechSynthesis`, y escucha la
respuesta con la Web Speech API — el mismo camino que ya usa el copiloto, sin
audio del lado del servidor.

Falta una pieza del lado del intérprete. `InterpretadorDeTexto` hoy entiende
**preguntas**: rangos, palos, stacks, spots, situaciones, formatos y manos.
Entrenar necesita que entienda **respuestas**, y esas formas ya están en
`acciones.json` como los `dichos` de cada acción ("all in", "shove", "pagar",
"tirar"): las 15 acciones los tienen. O sea, una categoría más que sale del
registro, sin listas nuevas en código, fiel a la regla del proyecto.

El intérprete necesita saber en qué modo está: entrenando, "all in" es una
respuesta y no hay que buscarla entre los spots. Es el mismo mecanismo que
`NivelDeDictado` —acotar contra qué categoría se busca— y por la misma razón:
sin acotar, las categorías compiten por las mismas palabras.

> **Nota de 2026-08-28.** El spec original decía que había que agregarle un
> segundo modo a `GeneradorDeGramatica`. Esa pieza es de la gramática SRGS y
> quedó en `PokerProOS.Voz.Sapi`, que ya no está en la solución ni la
> referencia nadie. El reemplazo es el párrafo de arriba.

## Decisiones

**Las manos mixtas cuentan por cualquiera de sus partes.** Si `AA` es
`CALL 50 / RAISE_X2 50`, responder cualquiera de las dos es acertar, y el
veredicto muestra el reparto. Elegir una como "la correcta" sería inventar una
estrategia que la tabla no declara.

**El progreso va a base de datos, no a JSON.** Rompe a propósito una regla que el
proyecto venía sosteniendo —la app funciona sin SQL Server y solo pierde el
historial de voz—. Un calendario de repetición que se pierde no es un calendario,
y con varios usuarios un archivo compartido no sirve. Las tablas y la voz siguen
funcionando sin base, como hasta ahora; **el entrenador no**, y lo dice en
pantalla en vez de fallar callado.

## Arquitectura

Un slice nuevo `Entrenador/`, siguiendo la organización existente (`Tablas/`,
`Voz/`, `Bitacora/`). Las dos piezas con lógica de verdad quedan **puras**: sin
base, sin HTTP, sin reloj propio (la fecha entra como parámetro).

| pieza | capa | responsabilidad |
|---|---|---|
| `CalendarioDeRepeticion` | Application | progreso + resultado → progreso nuevo |
| `PlanificadorDeTanda` | Application | vencidas + catálogo + filtro + tamaño → preguntas |
| `IProgresoDeEntrenamiento` | Application | puerto: leer vencidas, leer una, guardar |
| `ResponderRespuestaHandler` | Application | resuelve la correcta, compara, actualiza, arma la ficha |
| `ProgresoDeEntrenamientoSql` | Infrastructure | el puerto contra EF, con `UsuarioId` en el índice único |
| `EntrenadorController` | Api | `POST /api/entrenador/tanda`, `POST /api/entrenador/respuesta` |
| `features/entrenador/` | React | mesa, botones y veredicto; reusa `FichaDeMemoria.tsx` |

La respuesta correcta la resuelve **`ResolverManoHandler`**, el mismo que contesta
por voz. No hay una segunda fuente de verdad sobre qué dice la tabla.

## Cómo se prueba

Con TDD, como el resto del proyecto:

- `CalendarioDeRepeticion`: tests puros de la escalera de intervalos, del reset al
  fallar y del vencimiento por fecha.
- `PlanificadorDeTanda`: catálogo sintético, como ya hace `AnalizadorDeMemoriaTests`;
  que lo vencido vaya primero y que el relleno priorice bordes.
- `ResponderRespuestaHandler`: puerto en memoria; acierto, fallo, y mano mixta
  aceptando las dos partes.
- `ProgresoDeEntrenamientoSql`: `ContextoEnMemoria`, como `SincronizadorTests`.

## Lo que no entra

Login y registro. Tablas por usuario. Vocabulario por usuario. Cobro. El
entrenador queda **preparado** para lo primero —el usuario está en la clave— y no
construido.

(La voz en el navegador figuraba acá y ya está hecha.)
