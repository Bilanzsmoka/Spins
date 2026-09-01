# Qué hace PokerProOS

Encargo para el mockup. Todo lo que la aplicación hace hoy, para que quien lo
diseñe no tenga que adivinar nada.

---

## El encargo, en un párrafo

Diseñá el mockup de **PokerProOS**, una aplicación de escritorio para estudiar
póker *Spin & Go*. La usa una sola persona, todos los días, para memorizar
tablas preflop hasta que la decisión salga sola en la mesa. Tema oscuro, look de
instrumento —no de página web—, denso en datos pero silencioso: **el único color
saturado de la pantalla son las acciones de póker**. Tiene diez módulos en un
menú lateral fijo; los dos que importan son la **grilla de 169 manos** y el
**entrenador con mesa dibujada**. Todo el texto va en castellano rioplatense.

---

## La regla que ordena todo

La grilla de 169 manos se pinta con los colores de las acciones, y ésa es la
memoria visual que el usuario está entrenando. **Si el resto de la interfaz
también usa color, la grilla deja de ser lo único que resalta y se pierde la
señal.**

Por eso el acento de la app no es un tono: es un gris casi blanco. Lo que
destaca, destaca por contraste y por peso. Los únicos botones de color son
ALL-IN, CALL y FOLD, porque ésos *son* el vocabulario del juego.

### Los colores de las acciones — no se tocan

| Acción | Color |
|---|---|
| ALL-IN | `#4CAF50` |
| CALL / CHECK | `#FFB74D` |
| RAISE (todas las medidas) | `#7986CB` |
| FOLD | `#E0E0E0` |

### Los tokens de la interfaz

| Rol | Valor | Dónde |
|---|---|---|
| Fondo | `#0b0e13` | toda la app |
| Panel | `#12161d` | tarjetas, listas, mesa |
| Línea | `#222833` | divide; casi nunca encierra |
| Línea fuerte | `#2e3644` | contornos de botón |
| Texto | `#e9eef5` | — |
| Apagado | `#8b95a5` | gris con una pizca de azul |
| Tenue | `#616c7d` | bajadas, etiquetas |
| Acento | `#dfe6f0` | no es un color: es casi blanco |
| Estado | `#22c55e` · `#f59e0b` · `#ef4444` | bien / cuidado / mal, nada más |
| Paño | `#16412f` → `#0f3023` | sólo la mesa del entrenador |
| Riel de la mesa | `#242d37` | borde de 13 px del óvalo |

### Tipografía

- **Archivo** para la interfaz.
- **IBM Plex Mono** para *todo número y toda mano* — stacks, ciegas,
  porcentajes, segundos y las 169 casillas.

Esa única regla es lo que hace que se lea como un instrumento.

---

## El menú

Barra lateral fija de 232 px, agrupada por área. El módulo activo se marca con
una regla de 2 px al costado, no con una pastilla rellena. Cada ítem tiene
nombre y una línea de qué hace.

```
SPINS          Entrenamiento     Tablas preflop y copiloto
               Entrenador        Te pregunta y te corrige
               Cómo venís        Aciertos y lo que peor te sale
               Diario            Tu día y tu evolución
               Hábitos           Cumplimiento y efecto
               Sesiones          Próximamente

EL JUEGO       Diccionario       Qué significa cada palabra
               Tipos de jugador  Contra quién es cada tabla

AJUSTES        Voz               Cómo decís vos cada cosa

BANCA          Bankroll          Próximamente
```

---

## Las pantallas

### Entrenamiento — la pantalla de consulta

- Cuatro selectores en línea: **formato** (HU / 3-max), **situación**,
  **stack**, **spot**.
- La **grilla de 13×13**: 169 manos, pares en la diagonal, suited arriba,
  offsuit abajo. Cada casilla pintada con el color de su acción. Debajo, la
  leyenda con cuántas manos toca cada acción.
- Un **copiloto de voz**: el micrófono escucha siempre y se le dicta una mano
  hablando —«be be contra limp, as rey suited»—; contesta en voz alta y resalta
  la casilla.
- Lista de **consultas** hechas, y otra de **frases que no entendió**, donde se
  le enseña cómo se dice cada cosa.
- Al tocar una casilla se abre la **ficha de memoria**. Un modo **corregir
  tabla** permite cambiar una casilla y escribir el porqué del spot.
- Un cartel rota cada 10 minutos con la **explicación** de una situación al azar.

### Entrenador — la pantalla más importante

- **Dos columnas.** Izquierda: la mesa y los botones, fijos. Derecha: lo que
  aparece al contestar, con su propio scroll. **Nada de scroll en la página.**
- **La mesa**: paño ovalado con riel, vos abajo y los rivales enfrente. Cada
  silla lleva posición, silueta con la figura de su tipo de jugador, qué hizo y
  su stack. El botón del dealer va pegado a su silla. En el paño, las fichas de
  cada uno con el monto en ciegas.
- Tus **dos cartas** grandes, boca arriba, **sin decir el nombre de la mano**:
  en una mesa se leen las cartas.
- **Botones de acción** con el color de la tabla y su tecla escrita: `A` `S`
  `D` `W`.
- Un **reloj** que cuenta hacia arriba y se pone ámbar a los 5 s y rojo a los 10.
- **Acertar no muestra nada**: la mesa se enmarca en verde y entra la mano
  siguiente sola.
- **Fallar** muestra, a la derecha: el veredicto, la grilla del spot con la mano
  resaltada, y la ficha de memoria. La voz dice qué era y la regla del grupo.
- Debajo, **lo que va de la tanda**: cada mano con lo que contestaste, lo
  correcto y cuánto tardaste.
- Antes de arrancar, **lo que más te cuesta**: los errores que se repiten igual.
- Filtro plegable: formato, situación, spot, rango de stack y cantidad de manos
  (5, 10, 20, 40 o sin límite).

### Cómo venís — estadísticas

- Cuatro cifras grandes: **manos jugadas**, **% de aciertos**, **bien
  contestadas**, **tiempo por mano**.
- Lista de **spots de peor a mejor**, con el porcentaje en verde/ámbar/rojo y un
  botón que abre el entrenador ya filtrado en ese spot.

### Hábitos y Diario — la disciplina

- **Panel de hoy**, arriba de todo: volumen jugado contra la meta, si
  estudiaste, el **hito activo** con su barra, y la semana en siete puntos con
  la regla «sin dos días seguidos».
- Cuadro de hábitos por día, con el cruce contra cómo jugaste.
- Diario: una entrada por día con intención, autocalificación A/B/C, disparador
  de tilt, mesas, minutos y notas.

### Diccionario y Tipos de jugador — el vocabulario

- 33 términos en cinco grupos, cada uno con un **play** que lo lee en voz alta.
- Nueve **perfiles de rival** en fichas: círculo de color con su figura,
  etiqueta, dos o tres señales para reconocerlo y su explicación. Separados en
  dos ejes: qué tan fuerte es (semáforo: verde plata, ámbar cuidado, rojo
  peligro) y cómo juega (colores fríos, de cerrado a suelto).

### Voz — ajustes

- Todas las categorías de vocabulario con sus formas habladas, y un botón
  **grabar** por entrada: se dice cómo se nombra cada cosa y queda aprendido.

---

## La ficha de memoria

Aparece al tocar una casilla y al fallar en el entrenador. Es lo que convierte
un «incorrecto» en algo que se entiende, y conviene que el mockup la trate como
una pieza propia.

- **La mano ancla**: hasta dónde llega el bloque que comparte su acción.
- **En este spot**: hasta tres frases que resumen la tabla —«los Ax offsuit:
  todos ALL-IN», «los Kx offsuit: ALL-IN hasta K7o»—.
- **Según el stack**: la misma mano a través de todas las bandas.
- **Las familias emparentadas** y el **peso en combos** de cada acción del spot.
- **La línea**: los spots del stack en el orden en que ocurren.
- El **tip** escrito a mano, si lo hay.

---

## Lo que hay que respetar

| Qué | Por qué |
|---|---|
| Los colores de las acciones | son la memoria visual que se entrena; no se cambian ni se repiten en el resto de la interfaz |
| La mesa no dice el nombre de la mano | en una mesa se leen dos cartas, no un código |
| Todo monto va en ciegas, con número | en póker lo que se mira son las fichas |
| Sin scroll en el entrenador | lo que hay que leer rápido no puede estar a un scroll de distancia |
| Un botón relleno por pantalla | diez botones rellenos en fila dejan de destacar |
| Los números en mono | se comparan en columna: stacks, porcentajes, tiempos |

---

Escala real: **17 tablas, 339 spots, 57.291 casillas**. La app funciona hoy
—esto no es una idea, es lo que hay— y el mockup es para verla mejor, no para
inventarle funciones.
