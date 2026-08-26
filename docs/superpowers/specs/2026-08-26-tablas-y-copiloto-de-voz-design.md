# Tablas preflop y copiloto de voz

Fecha: 2026-08-26
Estado: aprobado para planificar

## Objetivo

El usuario entrena en PokerHero con las manos y los ojos ocupados. Esta aplicación es el copiloto que responde por voz: se le dicta una mano y contesta hablando qué acción corresponde, mientras resalta la celda en la grilla.

La meta no es reemplazar a PokerHero. Es tener las tablas propias al alcance de la voz para memorizarlas con la práctica.

## Alcance

Entra:

- Consultar tablas: grilla 13x13, selectores de situación / stack / spot, conteos por acción, leyenda.
- Copiloto de voz local: escucha continua, respuesta hablada, resaltado visual.
- Validación de las tablas al arrancar, con reporte legible de lo que esté mal.

No entra en esta versión:

- Drill de entrenamiento con manos al azar.
- Registro de sesiones de spins e importación de GG.
- Bankroll, monedas, importación de manos de PPPoker.

Esos módulos llegan después. La estructura los contempla; el código no los adelanta.

## Principio rector: nada hardcodeado

La aplicación no declara en código qué situaciones, stacks, spots ni acciones existen. Los descubre de los datos.

El estado actual viola esto en cuatro lugares:

| Hardcodeado hoy | Dónde | Pasa a |
| --- | --- | --- |
| Lista de 11 stacks | `frontend/src/core/constants/poker.ts` | Derivada del catálogo |
| Colores y etiquetas por acción | `frontend/src/core/constants/poker.ts` | Registro de acciones |
| Enums `Actions`, `Situation`, `Spot` | `Domain/Enums/` | Se eliminan |
| Situación `HU_SB_OR_FISH` literal | `EvaluateAnswerHandler` | Parámetro de la consulta |

Criterio de aceptación del principio: agregar una tabla que use una acción nueva (por ejemplo `LIMP`) debe funcionar de punta a punta — grilla coloreada, leyenda, gramática de voz, respuesta hablada — sin modificar una línea de código.

## Datos

### Fuente de verdad

Los archivos JSON en `database/seed-data/` son la fuente de verdad. Viven en git, se revisan en un diff y se copian a otra máquina con la carpeta. Son el activo irreemplazable del proyecto.

El formato actual se conserva sin cambios: situación, luego `stacks[]`, luego `spots[]`, luego `actions{}`, donde cada acción mapea a un arreglo de manos o al literal `REST`, que se expande a todas las manos no asignadas.

### Registro de acciones

Nuevo archivo `database/registro/acciones.json`. Define cada acción una sola vez: clave canónica, etiqueta visible, color de fondo, color de texto, orden en la leyenda y las formas en que se la puede decir en voz alta.

Los colores son los del proyecto original, que es la memoria visual ya entrenada del usuario:

| Acción | Color | Nota |
| --- | --- | --- |
| `ALL-IN` | `#43bf55` | verde |
| `CALL` | `#ffb743` | ámbar |
| `RAISE_X2` | `#7c86dc` | violeta |
| `FOLD` | `#edf3fb` | blanco |

Los colores actuales del proyecto están invertidos respecto de estos y deben corregirse: hoy `ALL-IN` es azul, `CALL` es verde y `FOLD` es rojo.

### Registro de vocabulario

Nuevo archivo `database/registro/vocabulario.json`. Traslada el `voice-dictionary.md` del proyecto anterior a datos ejecutables: rangos con sus formas habladas en español e inglés, palos, y las frases que identifican cada spot y cada situación.

Los spots se declaran acá una vez y no se repiten en los once archivos de tablas.

### Lo que se descubre y no se declara

- Situaciones, stacks y spots: del catálogo.
- Rango de BB de cada stack: de `minBB` y `maxBB`.
- Manos de la matriz: generadas de los 13 rangos. Es la única constante legítima, porque el póker tiene 13 rangos y eso no cambia.

## Arquitectura

Se conservan las cuatro capas, con la dirección de dependencias intacta: Domain, luego Application, luego Infrastructure, luego Api.

```
Domain          ManoTexto, RangoDeStack, CeldaDeTabla
                Sin enums de dominio: las acciones son datos.
                SpinSession, SpinTournament y TrainerAttempt quedan
                donde están, intactas, esperando a sus módulos.

Application     Tablas/  ICatalogoDeTablas, IRegistroDeAcciones,
                         ResolverManoHandler, ConsultarTablaHandler
                Voz/     IReconocedorDeVoz, ISintetizadorDeVoz,
                         InterpretarDictadoHandler, MemoriaDeContexto

Infrastructure  Tablas/  CatalogoEnMemoria, CargadorDeJson, ValidadorDeTabla,
                         SincronizadorABaseDeDatos
                Voz/     ReconocedorSapi, SintetizadorSapi, GeneradorDeGramatica
                Datos/   PokerProOSDbContext, migraciones

Api             TablasController, VozHostedService, endpoint SSE
```

`IReconocedorDeVoz` e `ISintetizadorDeVoz` se declaran en Application. SAPI queda confinado a Infrastructure, para que cambiar el motor de voz sea reemplazar una carpeta y no rastrear llamadas por todo el proyecto.

## Los tres caminos del dato

```
  JSON en git ──── valida al arrancar ────┬──→ Catálogo en memoria
  (fuente de verdad)                      │     (lectura de la voz)
                                          │
                                          └──→ Base de datos
                                                (espejo + datos mutables)
```

**Memoria** es el camino de lectura del copiloto. La consulta hablada no viaja a SQL Server: el requisito es respuesta instantánea y un viaje a la base por cada mano dictada trabaja en contra.

**Base de datos** guarda el espejo de las tablas y todo lo mutable. Tener las celdas ahí es lo que va a permitir cruzar "qué manos fallo más" contra "en qué tablas caen", que es un JOIN y no se hace contra un archivo.

En esta versión la base tiene dos trabajos concretos, no es decorativa:

1. **Espejo de las tablas**, sincronizado desde los JSON al arrancar.
2. **Bitácora de consultas de voz**: qué mano se preguntó, en qué stack y spot, qué se contestó y cuándo. Es una tabla y un insert, pero es el dato que dentro de un mes responde "las manos que más consulto son las que menos sé" — que es exactamente el leak que hay que estudiar. Sin esto habría que empezar a juntar el dato desde cero más adelante.

Los intentos del entrenador, la accuracy y las sesiones llegan con sus módulos.

Tres correcciones sobre cómo se llena hoy:

1. **Migraciones EF en lugar de `EnsureCreated`.** Hoy cualquier cambio de entidad obliga a borrar la base entera. Un módulo que va a crecer necesita que el esquema evolucione sin perder el historial.
2. **Validar antes de sincronizar.** Hoy el importador escribe lo que reciba. Lo que no pasa la validación no entra.
3. **Degradación limpia.** Si SQL Server no está disponible, la aplicación arranca igual con el catálogo en memoria y avisa en pantalla que no hay historial. Una herramienta de estudio no puede caerse porque un servicio de Windows no levantó.

## Validación de tablas

`ChartValidator` es hoy un cascarón que siempre devuelve válido. Se implementa de verdad y corre al cargar cada archivo:

- Cobertura exacta de 169 manos por spot.
- Sin manos duplicadas entre acciones del mismo spot.
- Toda etiqueta de mano existe en la matriz.
- Toda acción usada existe en el registro de acciones.
- Como máximo un `REST` por spot.
- Si el archivo declara `expectedCounts` o `checks`, deben cuadrar.

Ante un archivo inválido: **carga el resto y marca ese como inválido en la interfaz**, indicando archivo, stack, spot y qué falta. No aborta el arranque. El usuario va a estar subiendo tablas de a poco y un error de tipeo no puede dejarlo sin herramienta.

Estado verificado al momento de escribir este spec: los once archivos actuales pasan las seis reglas. Cubren de 1bb a 99bb; los stacks de 6bb en adelante tienen los cinco spots, y 1-4bb y 5bb tienen tres, que es lo esperado a esos stacks.

## El bucle de voz

```
Gramática generada del catálogo y los registros
              ↓
    Escucha continua  ←──── watchdog reinicia si Windows la corta
              ↓
    "siete bb a cinco offsuit"
              ↓
    InterpretarDictado → { stack: 7bb, spot: SB_OR, mano: A5o }
              ↓
    Resolver contra el catálogo → ALL-IN, 113 manos en el rango
              ↓
    Hablar  ←──── el reconocedor se pausa para no oírse a sí mismo
              ↓
    SSE → el front resalta A5o en la grilla
```

### Motor

`System.Speech.Recognition` con gramática restringida, sobre el reconocedor `MS-3082-80-DESK` (es-ES) ya instalado en la máquina. Síntesis con las voces es-ES locales: Helena, Laura, Pablo.

Verificado antes de escribir este spec: `System.Speech` carga sobre .NET 10.0.11 y el reconocedor en español está presente. Cero megabytes de descarga, sin internet.

La decisión de gramática restringida sobre dictado libre es deliberada: el vocabulario es cerrado — trece rangos, dos palos, números de stack, cinco spots — y restringirlo sube muchísimo la precisión y baja la latencia, porque no hay modelo neuronal en el camino. "A cinco offsuit" debe caer siempre en `A5o`, no interpretarse creativamente.

Costo aceptado: ata el proyecto a Windows. Mitigado por la interfaz `IReconocedorDeVoz`.

### Gramática generada

La gramática se construye del catálogo más los registros, nunca de listas en código. Al subir una tabla de un stack nuevo, la voz lo entiende sin tocar nada.

El stack se resuelve por cobertura, no por igualdad de texto: decir "trece bb" encuentra la tabla `13-16bb` porque 13 cae dentro de su rango. Esto corrige de raíz el error de `EvaluateAnswerHandler`, que armaba la clave concatenando el número con `bb` y por lo tanto nunca podía encontrar ningún stack con rango.

### Memoria de contexto

Si el dictado solo trae la mano, se usan el stack y el spot activos en pantalla. Si trae stack o spot, se actualizan. Así no hay que repetir el contexto en cada consulta.

### Respuesta

Acción más un dato corto que ancle la memoria; alrededor de un segundo y medio:

> "ALL-IN. En el borde, 113 manos."

"En el borde" tiene una definición precisa, para que no se invente al implementar: **una mano está en el borde si alguna celda vecina en la matriz — el rango inmediatamente superior o inferior, en la misma fila o la misma columna — tiene una acción distinta.**

Es el dato que más vale memorizar. Que `A5o` sea ALL-IN importa poco si `A4o` también lo es; importa mucho si `A4o` es FOLD, porque ahí está la línea que hay que recordar. Si la mano está rodeada de la misma acción, se omite la frase y se contesta solo la acción con el conteo.

### Manos sin palo especificado

Una mano dictada sin calificar el palo es **offsuit**. Decir "a rey" significa `AKo`. Es la convención de quien dicta: cuando la mano es suited se dice, y si no se dijo, no lo es.

Las parejas no tienen palo, así que la regla no las toca: "as as" es `AA`.

El riesgo no está en la regla sino en el reconocimiento: si el motor se come la palabra "suited", el usuario recibe la acción del offsuit sin enterarse de la sustitución. Por eso, **cuando se aplicó el default la respuesta repite la mano interpretada**:

> Dictado: "siete bb a rey" → "A K offsuit: CALL."
> Dictado: "siete bb a rey suited" → "CALL."

Confirmar solo cuando se asumió mantiene rápido el caso explícito y hace audible el caso donde pudo haber una pérdida de palabra. La mano interpretada también queda escrita en pantalla, siempre.

## Errores

| Situación | Comportamiento |
| --- | --- |
| No entendió el dictado | Dice "no te entendí". No adivina la mano más parecida. |
| Spot inexistente en ese stack | Lo dice explícitamente. No inventa ni cae al spot por defecto. |
| Tabla inválida | Carga el resto y la marca en la interfaz con archivo y spot. |
| Reconocedor caído | El watchdog reinicia. Indicador visible de si está escuchando. |
| Base de datos no disponible | Arranca igual; avisa que no hay historial esta sesión. |

El indicador de escucha no es decorativo: si la aplicación se queda muda, el usuario tiene que verlo, no descubrirlo hablándole al vacío.

## Interfaz

Sobria, sin adornos. Paleta oscura del proyecto original: fondo `#0d1117`, panel `#151a21`, texto `#edf3fb`, acento `#8bb8e8`.

Elementos: grilla 13x13, selectores de situación / stack / spot, conteos por acción, leyenda, indicador de escucha y la última frase reconocida en pantalla, para que el usuario vea si lo escuchó mal.

Se conserva una regla del `architecture.md` anterior: **el color nunca es la única señal**. Cada celda lleva su etiqueta de mano además del color de fondo.

Los colores de acción se leen del registro. La leyenda se arma sola con lo que el registro declare.

## Pruebas

Hoy no existe ningún proyecto de pruebas. Se agrega uno.

Las tres suites principales son dominio puro, sin voz ni interfaz, y por lo tanto rápidas y deterministas:

- **Validador**: los once archivos reales deben pasar, más archivos rotos fabricados a propósito — mano duplicada, acción fuera del registro, dos `REST`, cobertura incompleta — que deben fallar con el mensaje correcto.
- **Intérprete de dictado**: el `voice-dictionary.md` convertido en tabla de casos, frase hablada contra consulta esperada. El diccionario es la especificación y la suite a la vez.
- **Resolución de manos**: los bloques `checks` que ya traen los JSON, más la resolución de stack por cobertura de rango, el default a offsuit cuando no se dicta el palo, y la detección de borde.

El reconocedor y el sintetizador quedan detrás de sus interfaces, así que la lógica del bucle de voz se prueba con dobles, sin micrófono.

## Decisiones tomadas

| Decisión | Motivo |
| --- | --- |
| Voz local con SAPI y gramática | Requisito explícito: rápido y sin internet. Vocabulario cerrado. |
| Tablas en JSON como fuente de verdad | Activo versionable y portable del proyecto. |
| Catálogo en memoria para la voz | La latencia es requisito; un viaje a la base por consulta lo rompe. |
| Base de datos desde el principio | Los módulos futuros la necesitan; retrofitear sale más caro. |
| Migraciones en vez de `EnsureCreated` | Que el esquema evolucione sin perder historial. |
| Registros en datos | Requisito explícito: nada hardcodeado. |
| .NET 10 | Es la última versión estable y es LTS, con soporte hasta noviembre de 2028. |

## Descartado

- **Whisper y Vosk**: buenos para dictado libre, innecesarios para cincuenta palabras. Whisper agrega latencia y entre 75 y 150 MB; Vosk unos 40 MB. La gramática de Windows no descarga nada y es más precisa en vocabulario cerrado.
- **Sin backend, todo en el navegador**: lo más rápido de levantar hoy, pero deja sin base a los módulos que vienen.
- **Tablas solo en base de datos**: obliga a que el activo del proyecto viva en un servidor en vez de en git.
- **Drill de entrenamiento en esta versión**: el entrenamiento ocurre en PokerHero.

## Deuda conocida que este trabajo remueve

- `WeatherForecast.cs` y `WeatherForecastController.cs`: andamiaje de `dotnet new`.
- `Domain/Enums/`: sin uso y en desacuerdo con los datos, donde el enum dice `ALL_IN` y el dato dice `ALL-IN`.
- `ChartValidator`: cascarón que siempre aprueba.
- Ruta de sembrado resuelta cinco directorios arriba de `AppContext.BaseDirectory`, que falla en silencio en una compilación publicada.
- Copia manual de `frontend/dist` a `wwwroot`, sin nada que la automatice.
