# Ficha de memoria

Fecha: 2026-08-26
Rama: `tablas-y-copiloto-de-voz`

## Problema

El copiloto responde la acción y, cuando la mano limita con otra acción, agrega
hablado *"En el borde, N manos"*. Las dos mitades de esa frase fallan:

- **"En el borde"** no dice contra qué limita. Una tabla tiene cuatro bordes; la
  palabra no distingue "es la última que sube de su fila" de "acá abajo empieza
  el fold". Escuchada en medio de una mano, no deja nada memorizable.
- **"N manos"** cuenta casillas de la grilla, no manos de la baraja. Una casilla
  suited son 4 combos, una offsuit 12, un par 6. Un rango cargado de suited ocupa
  menos baraja de lo que la grilla aparenta, así que el número engaña justo en el
  sentido contrario al que sirve para calcular.

Además la app responde el spot consultado y nada más: no dice qué hacer si el
rival 3-betea o va all-in, aunque el JSON ya guarda esos spots en orden.

## Vocabulario

De cómo estudian estos rangos los coaches (ver Fuentes), tres conceptos:

- **Mano ancla** (*bottom of range*): en vez de memorizar mano por mano, se
  aprende la última que entra. Sabiendo que A8o es el fondo, A9o..AKo se deducen.
- **Umbral de stack**: la mano descrita como pares acción/corte. La notación de
  ejemplo es `HUSB AA → L 4bb+ / OS 4bb-`.
- **Familias**: pares, ases suited, broadways, conectores suited. Reglas que
  explican muchas manos de una.

Y el principio de retención: se recuerda mejor lo que se entiende. De ahí que el
*porqué* estratégico sea la única pieza escrita a mano.

## Qué se construye

Una **ficha de memoria**: seis piezas sobre la mano consultada, cinco deducidas
del catálogo y una escrita a mano.

### 1. `AnalizadorDeMemoria` (Application/Tablas)

Recibe situación, stack, spot, mano y acción ya resueltos. Devuelve `FichaDeMemoria`.

**Ancla.** La familia de la mano es: los pares, o `{rango alto}x suited`, o
`{rango alto}x offsuit`. Se recorre por kicker descendente (pares: por rango
descendente) y se ubica el bloque contiguo que contiene la mano y comparte su
acción. Se reportan sus extremos y las manos que lo rompen a cada lado.
Ejemplo: A8o a 17-18bb SB_OR → *"de A9o para arriba suben; A8o es la primera que
paga"*. Si la familia entera comparte la acción no hay ancla que reportar y la
pieza sale vacía.

**Umbral.** La misma mano, en el mismo spot, a través de todos los stacks de la
situación, ordenados por `minBB` y colapsados en bandas contiguas de igual acción.
Ejemplo: A8o SB_OR → *"ALL-IN ≤16bb · CALL 17-18bb · RAISE X2 19bb+"*. Un stack
que no declara ese spot se salta; si eso parte una banda, quedan dos bandas.

**Familias.** En el spot actual, el ancla de cada familia que tenga más de una
acción. Ejemplo a 17-18bb SB_OR: *"ases suited hasta A7s · ases offsuit hasta A9o
· pares hasta 55"*. Las familias uniformes no se listan.

**Peso.** Por cada acción del spot, su porcentaje de la baraja medido en combos.
El de la acción de la mano consultada va destacado. Una celda mixta reparte sus
combos entre sus acciones según la frecuencia declarada (una casilla offsuit al
50/50 aporta 6 combos a cada acción), así los porcentajes siguen sumando 100.

**La línea.** Los otros spots del mismo stack, en el orden en que el JSON los
declara — que ya es el orden en que ocurren en la mano (`SB_OR`,
`VS_BB_ALL_IN`, `VS_BB_3BET`, `VS_BB_ISO_3BB`, `VS_BB_ISO_ALL_IN`) — con la
acción de esa misma mano en cada uno. Un stack con un solo spot devuelve la línea
vacía.

### 2. Combos (Domain)

`MatrizDeManos` gana `Combos(string mano)` y `CombosTotales`.

CLAUDE.md permite como constantes desnudas sólo los 13 rangos y el 169. No se
agregan 4, 6, 12 ni 1326: se agrega `PalosPorRango = 4` y el resto se deriva —
par = C(4,2) = 6, suited = 4, offsuit = 4×4 = 12, total = C(52,2) = 1326. Es
aritmética de la baraja, no configuración; por eso vive en Domain y no en
`database/registro/`.

### 3. Tip escrito a mano

Un spot puede declarar `"tip": "…"`: el porqué estratégico, que ningún cálculo
deduce. `ValidadorDeTabla` sólo verifica que, si la clave existe, el texto no esté
vacío. Los 13 archivos actuales siguen cargando sin tocarlos.

Es la única pieza que se desactualiza al cambiar manos, y por eso la única que
escribe el usuario.

Se edita desde la pantalla, no abriendo el JSON a mano: la ficha muestra el tip
con un botón para editarlo, y guardar reusa `IEditorDeTablas`, que ya escribe el
archivo y recarga el catálogo en caliente. El JSON sigue siendo la fuente de
verdad; la pantalla es sólo otra forma de escribirlo.

### 4. API

`ResultadoDeConsulta` gana `Ficha`, y el evento SSE de `/api/voz/eventos` la
lleva: al dictar, la ficha aparece sola.

Nuevo `GET /api/tablas/ficha?situacion=&stack=&spot=&mano=`, para tocar cualquier
celda de la grilla y leer su ficha sin dictar — estudiar sin micrófono.

Nuevo `PUT /api/tablas/{situacion}/{stack}/{spot}/tip`, con el texto en el cuerpo.
`IEditorDeTablas` gana `EditarTipAsync`, hermano de `EditarAsync`: misma escritura
al JSON y misma recarga en caliente. Texto vacío borra la clave `tip` del archivo.

### 5. Voz

`RedactorDeRespuesta` deja de agregar `" En el borde, {N} manos."`. La voz queda
en la acción sola; el mix se dice como hoy. `RespuestaDeMano.EnElBorde` se
conserva porque la pantalla lo usa para resaltar, pero no se habla más.

### 6. Pantalla

Un **popup** por mano, no un panel al costado: `FichaDeMemoria.tsx` se abre
centrado sobre un fondo oscurecido al tocar una celda de la grilla o al dictar
una mano. Se cierra con el botón, con Escape y tocando el fondo.

Cabecera: la casilla pintada con el color exacto de su acción —el mismo cuadro
que en la grilla, para que el ojo lo reconozca—, la mano en grande, la etiqueta
de la acción y su peso de baraja.

Cuerpo, en orden de lectura: ancla → umbral (barra de stacks) → familias → la
línea → tip.

El popup es también el único lugar donde se configura esa mano. `EditorDeCelda`
deja de renderizarse debajo de la grilla y pasa adentro del popup: cuando
"Corregir tabla" está activo, sus controles de acción y mix aparecen bajo la
ficha. El tip se edita ahí mismo —un botón lo convierte en campo de texto y
guardar llama al `PUT`— y un spot sin tip muestra el botón igual, para escribir
el primero. Las otras cinco piezas son de sólo lectura: se cambian cambiando la
tabla.

## Qué NO se construye

- No se muestran las otras situaciones del catálogo (BB vs limp, BB vs min-raise)
  para la misma mano. El alcance es la línea del stack actual.
- No se muestra la mano en todos los stacks como tabla: el umbral ya la resume.
- La voz no lee la ficha, ni bajo pedido. No se toca la gramática SAPI.
- No se muestra el porcentaje por casillas junto al de combos.
- La ficha no vive como panel fijo al costado de la grilla: es un popup.

## Pruebas

**Domain.** `Combos` para par, suited y offsuit; `CombosTotales` = 1326; la suma
de los combos de las 169 manos da `CombosTotales`.

**Application (`AnalizadorDeMemoria`).**
- Ancla en medio de la fila (A8o a 17-18bb SB_OR).
- Ancla en el tope: AKo, sin nada por encima.
- Familia uniforme: sin ancla, pieza vacía.
- Umbral con bandas no contiguas.
- Mano mixta.
- Stack con un solo spot (`BB_VS_SB_MR` a 1-5bb): línea vacía.
- Peso: los porcentajes de las acciones de un spot suman 100.

**Infrastructure.** Un archivo con `"tip"` vacío es un `ProblemaDeTabla`; uno sin
la clave carga sin problema. `EditarTipAsync` escribe la clave en el archivo del
spot correcto y deja el resto del JSON intacto; con texto vacío la borra.

## Fuentes

- [How to Memorize Poker Charts & Ranges](https://bitb-spins.com/articles/how-to-memorize-poker-ranges/) — manos ancla, familias, umbrales por stack, el porqué como motor de retención.
- [How To Study Preflop Ranges](https://pokercoaching.com/blog/how-to-study-preflop-ranges-and-poker-strategies/) — las dos fases: memorizar y después entender.
- [Push Fold Charts for Tournaments](https://upswingpoker.com/push-fold-tournament-strategy-charts/) — manos de fondo de rango como método.
