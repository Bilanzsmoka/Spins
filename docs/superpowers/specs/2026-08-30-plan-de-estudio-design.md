# El plan de estudio — diseño

**Fecha:** 2026-08-30
**Estado:** diseño aprobado en conversación; falta el plan de implementación.

## El problema

La app mide todo y no compara nada contra nada. El hábito `VOLUMEN` guarda
cuántos torneos jugaste; ningún lado dice que tu objetivo son 140. El
entrenador guarda casilla por casilla en qué escalón de repetición estás;
ningún lado dice cuánto falta para saber una tabla. Hay datos y no hay plan,
así que no se puede contestar la única pregunta que importa día a día:
**¿hoy voy bien?**

Palabras del usuario: *"si no tenemos un plan va a ser demorado"*.

## Lo que ya existe y NO hay que volver a construir

| Pieza | Dónde | Qué da |
|---|---|---|
| Hábitos definidos en datos | `database/registro/habitos.json`, `IRegistroDeHabitos` | `VOLUMEN` (numérico), `ESTUDIO` (binario), y el resto |
| Marcas diarias | `MarcaDeHabito` (fecha, clave, valor) | cuánto hiciste cada día |
| Resumen y cruce | `ProgresoDeHabitos` | cumplidos, días registrados, rachas, cruce con nivel de juego |
| Progreso del entrenador | `ProgresoDeCasilla` + `IProgresoDeEntrenamiento.TodasAsync` | por casilla: aciertos seguidos, intervalo en días, vencimiento |
| Escalera de repetición | `CalendarioDeRepeticion.Escalera` = `[1,3,7,16,35,90]` | qué significa cada escalón |
| Regla de borde | `SpotDeTabla.EnElBorde` | si la mano está en el filo de su bloque |
| Filtro de tanda | `FiltroDeTanda(Formato, Situacion, …)` | el entrenador ya sabe preguntar sólo de una situación |
| Pantalla de hábitos | `frontend/src/features/diario/PaginaDeHabitos.tsx` | dónde va el panel nuevo |

## La medición, hecha antes de diseñar

Contado sobre `database/seed-data/` con la misma regla que `EnElBorde` —alguna
vecina de la matriz tiene otra acción, o la celda es mixta—:

| Formato | Spots | Casillas | Bordes |
|---|---|---|---|
| 3-max | 240 | 40.560 | 8.181 |
| HU | 99 | 16.731 | 5.029 |
| **Total** | **339** | **57.291** | **13.210** |

Por situación, de menor a mayor esfuerzo:

| Formato | Situación | Spots | Bordes | Días a 20/día |
|---|---|---|---|---|
| 3-max | BB vs BTN limp | 9 | 357 | 17 |
| 3-max | BB vs 3-way limp | 9 | 368 | 18 |
| 3-max | SB vs BTN min-raise | 10 | 388 | 19 |
| 3-max | BB vs 3-way min-raise | 9 | 415 | 20 |
| 3-max | BB vs SB open shove | 30 | 533 | 26 |
| 3-max | SB vs BTN open shove | 33 | 546 | 27 |
| 3-max | BB vs SB min-raise | 13 | 559 | 27 |
| 3-max | SB vs BTN limp | 11 | 565 | 28 |
| 3-max | BB vs BTN open shove | 33 | 609 | 30 |
| 3-max | BB vs BTN min-raise | 18 | 651 | 32 |
| 3-max | BB vs SB limp | 15 | 709 | 35 |
| 3-max | BTN OR | 19 | 1.021 | 51 |
| 3-max | SB vs BB | 31 | 1.460 | 73 |
| HU | BB vs min-raise | 18 | 642 | 32 |
| HU | BB vs open shove | 12 | 642 | 32 |
| HU | BB vs limp | 18 | 1.004 | 50 |
| HU | SB OR | 51 | 2.741 | 137 |

**Esta tabla es la decisión de diseño más importante del documento.** Un hito
por formato —"las tablas de 3-max al 90%"— son 7.363 casillas de borde: más de
un año a 20 por día. Un hito **por situación** son 2 a 10 semanas. Por eso los
hitos son por situación, y son 17.

## El diseño

### Un hito

Un objetivo con nombre, un número y una barra. Uno activo a la vez; al
cumplirlo se prende el siguiente. Todos viven en
`database/registro/plan.json`, en el orden en que se recorren.

```json
{
  "metaDeVolumen": 140,
  "hitos": [
    {
      "clave": "3MAX_BB_VS_BTN_LIMP",
      "titulo": "3-max · BB vs BTN limp",
      "tipo": "saber",
      "situacion": "3MAX_BB_VS_BTN_LIMP_FISH_FISH",
      "escalonMinimo": 16,
      "objetivo": 90
    },
    {
      "clave": "VOLUMEN_DIARIO",
      "titulo": "140 torneos por día, dos semanas",
      "tipo": "jugar",
      "habito": "VOLUMEN",
      "objetivo": 140,
      "dias": 14
    }
  ]
}
```

Dos tipos, y nada más:

- **`saber`** — apunta a una `situacion` del catálogo. Se cumple cuando el
  `objetivo`% de sus **casillas de borde** están en un intervalo de
  `escalonMinimo` días o más. `escalonMinimo` es un valor de
  `CalendarioDeRepeticion.Escalera`; 16 significa cuatro aciertos seguidos
  separados en el tiempo, que no se finge.
- **`jugar`** — apunta a un `habito` numérico. Se cumple cuando en los últimos
  `dias` días se alcanzó el `objetivo` **sin fallar dos días seguidos**.

Un hito de tipo desconocido, o que apunta a una situación o un hábito que no
existe, es un problema del plan: se muestra en pantalla con su causa y no
frena a los demás, igual que `ProblemaDeTabla`.

### Por qué el denominador son los bordes y no las 169

El entrenador ya enseña los bordes primero, porque son los que separan saber
la tabla de adivinarla; el interior de un bloque se sabe sabiendo dónde
termina. Medir sobre las 169 haría el hito cinco veces más largo sin medir
nada más. La pantalla dice el número absoluto —"241 de 357 bordes"— para que
el porcentaje no se lea como algo que no es.

### Consistencia: nunca dos días seguidos

Un estudio sobre seguimiento de hábitos encontró que quien mide **días
seguidos** tiene **63% más probabilidad de abandonar el hábito por completo
después de fallar un día**, y que la regla *"nunca dos días seguidos sin"*
sostiene los hábitos **37% más tiempo**. La app hoy muestra `RachaActual` y
`MejorRacha`, que es exactamente el mecanismo que hace largar.

Los hitos de tipo `jugar` usan la regla de los dos días, no la racha. El
cuadro de hábitos existente queda como está en esta entrega —es historia, no
el motor diario—; despintar la racha es una entrega aparte.

### La pantalla: HOY

Arriba de todo en **Hábitos**, que es donde el usuario pidió que estuviera. Si
necesita scroll, falló.

```
  HOY · martes                                    día 12 del plan

  Volumen      ###########....   96 / 140 torneos
  Estudio      hecho: tanda del día
  Hito activo  3-max · BB vs BTN limp
               #######........   241 / 357 bordes   (68 / 90%)   [ Entrenar ]

  Esta semana  L  M  M  J  V  S  D
               ok ok hoy .  .  .  .        sin dos seguidos
```

**"Entrenar" es lo que convierte el plan en algo que se hace.** Abre el
entrenador con `FiltroDeTanda` puesto en la situación del hito activo. Ese
filtro ya existe: es pasar un parámetro, no código nuevo.

Los hitos cumplidos se listan abajo, tachados, con la fecha. Son el registro
de que avanzaste, que es medio punto del asunto.

## Arquitectura

Dirección de dependencias sin cambios: `Domain ← Application ← Infrastructure ← Api`.

| Archivo | Responsabilidad |
|---|---|
| `database/registro/plan.json` | los hitos y la meta de volumen |
| `Application/Plan/IRegistroDelPlan.cs` | `HitoDefinido`, `PlanDefinido`, el puerto |
| `Infrastructure/Plan/RegistroDelPlanJson.cs` | leerlo; vacío si falta, como el glosario |
| `Application/Plan/MedidorDeHitos.cs` | dado el catálogo, el progreso y las marcas, calcular cada hito |
| `Application/Plan/EstadoDelDia.cs` | el registro que viaja a la pantalla |
| `Api/Controllers/PlanController.cs` | `GET /api/plan/hoy` |
| `frontend/src/features/plan/PanelDeHoy.tsx` | el panel, montado arriba en `PaginaDeHabitos` |

`MedidorDeHitos` es puro: recibe catálogo, progreso y marcas ya cargados, y no
conoce base ni reloj —la fecha entra como parámetro—, igual que
`CalendarioDeRepeticion`. Así se prueba entero sin base.

El total de bordes de una situación se calcula del catálogo en memoria con
`EnElBorde`, que ya existe; no se guarda en ningún lado. Corregir una tabla
cambia el denominador en el acto, que es la conducta correcta.

## Flujo

1. La pantalla pide `GET /api/plan/hoy`.
2. El controlador junta el plan (JSON), el catálogo (memoria), el progreso del
   entrenador (SQL) y las marcas de hábitos (SQL).
3. `MedidorDeHitos` devuelve, para cada hito, cuánto lleva y si está cumplido;
   el primero no cumplido es el activo.
4. La pantalla dibuja el panel.

## Errores

- **Sin `plan.json`**: no hay plan, el panel no se dibuja y la pantalla de
  hábitos queda como hoy. No tumba el arranque — es material de estudio, como
  el glosario.
- **Sin SQL Server**: no hay progreso ni marcas, así que no hay plan que
  mostrar. Se muestra el error en pantalla, como hace el entrenador, y no se
  traga. Las tablas y la voz siguen andando.
- **Hito mal declarado**: se muestra con su causa, el resto del plan sigue.

## Pruebas

En `tests/PokerProOS.Tests/Plan/`, sin base:

1. Un hito `saber` con el 90% de sus bordes en escalón ≥ 16 está cumplido; con
   el 89%, no.
2. Casillas en escalón menor no cuentan, aunque estén contestadas.
3. El denominador son los bordes de esa situación, no sus 169 casillas ni las
   casillas contestadas: estudiar diez y acertarlas no da 100%.
4. Un hito `jugar` se cumple con los días alcanzados y un fallo suelto; **no**
   se cumple con dos fallos seguidos.
5. El hito activo es el primero no cumplido, en el orden del JSON.
6. Un hito que apunta a una situación inexistente se reporta como problema y
   no frena a los demás.
7. El plan real (`plan.json`) carga y todos sus hitos apuntan a situaciones y
   hábitos que existen — la misma prueba de integridad que tiene el glosario.

## Fuera de alcance

- **Los niveles de la sala** ($1 → $3 → $7) y la banca en buy-ins. Son la
  consecuencia del plan, no el plan. Se hacen después, y necesitan que exista
  dónde anotar la banca.
- **Un tracker de sesiones.** El hábito `VOLUMEN` ya alcanza para medir juego.
- **Crear hitos desde la pantalla.** Se editan en el JSON, como el vocabulario
  y el glosario antes de tener editor.
- **Despintar la racha** en el cuadro de hábitos existente.

## Fuentes

- Rachas y abandono: [Habit Streaks: Why They Work and When They Backfire](https://www.ehm-tech.com/habit/blog/habit-streaks-do-they-actually-work/)
- Indicadores que van adelante vs. atrás: [How to Use Leading and Lagging Indicators to Measure Learning](https://www.chameleoncreator.com/blog/how-to-use-leading-and-lagging-indicators-to-measure-learning)
- Rutina de estudio y rotación semanal: [The Ultimate Poker Study Routine](https://www.casino.org/blog/the-ultimate-poker-study-guide/)
