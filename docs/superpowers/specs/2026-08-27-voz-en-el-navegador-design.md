# La voz se muda al navegador

Fecha: 2026-08-27
Rama: `tablas-y-copiloto-de-voz`

## Problema

El copiloto reconoce mal. Medido sobre audio sintetizado, con el vocabulario y
las tablas reales:

| frase | confianza | qué hace |
|---|---|---|
| "reina nueve suited" ✓ | 0,62 | Q9s |
| "nueve be be" ✓ | 0,35 | cambia el stack |
| "contra el limite de gastos" ✗ | 0,45 | **cambia el spot a BB_VS_3WAY_LIMP** |
| "cuba" ✗ | 0,55 | **QK** |

Lo válido puntúa entre 0,35 y 0,67; lo falso, entre 0,32 y 0,55. **Las dos
poblaciones se pisan**, así que no existe un umbral que las separe: subirlo a
0,50 mata "nueve be be" y sigue aceptando "cuba".

La causa no es el umbral, son dos cosas del motor:

- **El modelo acústico de SAPI en español es de alrededor de 2010** y Microsoft
  no lo actualizó. Su propio dictado libre oye "Gana" por "dama" y "Hace ellos
  suite" por "as rey offsuit".
- **Una gramática SRGS está obligada a elegir**. No puede decir "esto no se
  parece a nada mío": ante "cuba" devuelve la entrada más parecida —`cu`, que es
  la reina— con confianza suficiente para pasar. No hay forma de rechazar.

Además el entrenamiento de voz de Windows no ayuda: se usa
`SpeechRecognitionEngine`, que corre in-process y no toma el perfil entrenado del
usuario (ese lo usa `SpeechRecognizer`, el compartido).

## Qué se construye

El navegador pasa a **oír y hablar**; el servidor sigue **entendiendo y
respondiendo**.

```
hoy:      microfono -> SAPI (oye + interpreta con SRGS) -> Copiloto -> tabla
despues:  microfono -> Chrome (oye) -> texto -> API -> Interprete -> Copiloto -> tabla
```

`CopilotoDeVoz`, `MemoriaDeContexto`, `ResolverManoHandler` y la ficha de memoria
**no cambian**: siguen recibiendo un `DictadoReconocido`. Lo único nuevo es quién
lo arma.

### El intérprete de texto

`InterpretadorDeTexto` (Application) recibe *"nueve be be reina nueve suited"* y
devuelve stack 9, alto Q, bajo 9, palo suited. Normaliza —minúsculas, sin
tildes— y compara contra el vocabulario, que sigue siendo la única fuente de
formas habladas.

**Exige un match bueno y rechaza si no lo hay.** Ahí está el arreglo de fondo: la
gramática SRGS no podía negarse, el intérprete sí. "cuba" deja de ser la reina y
pasa a ser lo que es, algo que no se dijo para la app.

Es código puro, sin audio ni HTTP: se prueba con decenas de frases escritas en
milisegundos, en vez de sintetizar wavs.

### La síntesis también se muda

No es simetría porque quede lindo. Hoy el reconocedor se pausa solo mientras la
app habla, para no oírse a sí misma y dispararse una consulta con su propia
respuesta. Si el servidor hablara por los parlantes mientras Chrome escucha por
el micrófono, esa protección desaparece: Chrome oiría la respuesta. Con los dos
en el navegador, se silencia el micrófono mientras habla.

### El contrato nuevo

`POST /api/voz/dictado` con `{ texto }`, que devuelve el mismo `EventoDeCopiloto`
que hoy viaja por SSE. El navegador manda lo que oyó; el servidor responde qué
hacer.

El SSE sigue existiendo: la pantalla puede estar en otra pestaña o dispositivo y
tiene que ver la respuesta igual.

El navegador escucha en modo continuo y manda **un POST por cada resultado
final** de Chrome. Los parciales no se mandan: son una frase a medio formar y
resolverlos daría respuestas contra manos que el usuario todavía no terminó de
decir.

### Los tres endpoints que cambian de dueño

- **`/api/voz/encender` y `/apagar`** hoy prenden y apagan el motor del
  servidor. Pasan a ser estado del navegador: encender es pedir el micrófono y
  arrancar el reconocimiento. El servidor deja de tener un motor que prender.
- **`/api/voz/estado`** hoy informa si el motor arrancó y su última falla. Pasa a
  reportar lo que sabe el navegador (permiso de micrófono denegado, sin
  conexión), que es donde ahora pueden fallar las cosas.
- **`/api/voz/capturar`** —el botón "dictá una forma nueva" de la página de
  vocabulario— usa hoy el dictado libre de SAPI, que es justamente el peor.
  Pasa a usar el mismo reconocimiento del navegador: el texto que Chrome oyó se
  ofrece como forma a agregar. **Esta es la parte que más mejora**: capturar cómo
  suena una persona diciendo "dama" era imposible con un motor que oye "Gana".

## Lo que desaparece

- `ReconocedorSapi`, `SintetizadorSapi` y `GeneradorDeGramatica`.
- `ServicioDeCopiloto`, el `BackgroundService` que escuchaba en el servidor.
- La gramática SRGS entera, con sus dos formas de frase.

El proyecto `PokerProOS.Voz.Sapi` **queda en el repo con su código**, pero sale
de `PokerProOS.slnx` y nadie lo referencia. Volver al motor viejo es un
`git revert`, no un interruptor: hay que devolverle al `Api` su
`net10.0-windows` y la referencia. Se conserva por si el navegador decepciona en
uso real, no como un modo alternativo mantenido.

### El Api deja de ser Windows-only

`PokerProOS.Api` apunta a `net10.0-windows` **solo** porque SAPI lo obliga. Sin
SAPI pasa a `net10.0` y la app corre en Linux o Mac. No es limpieza cosmética: es
el requisito de hosting del día que la use más de una persona, que el spec del
entrenador ya anticipa.

## Lo que se pierde

Hay que decirlo antes, no descubrirlo después:

- **Sin internet no hay voz.** Hoy funciona offline; Chrome manda el audio a
  Google.
- **Solo Chrome o Edge.** Firefox y Safari no implementan la API.
- **La pestaña tiene que estar abierta.** Si Chrome ralentiza el reconocimiento
  con la pestaña muy de fondo, se ve recién en uso real.
- **El audio sale de la máquina.** Va a los servidores de Google, como cualquier
  dictado de Chrome.

## Cómo se prueba

El cambio mejora la testabilidad, que es media razón para hacerlo:

- `InterpretadorDeTexto`: decenas de casos escritos —frases válidas, frases a
  medias, ruido de conversación— sin audio y en milisegundos. Hoy cada caso
  equivalente cuesta sintetizar un wav.
- Los casos que hoy fallan entran como pruebas: "cuba", "contra el limite de
  gastos" y "nueve de la noche" **deben rechazarse**.
- El endpoint, con el intérprete real y un copiloto de prueba.
- Lo del navegador (micrófono, silenciar mientras habla) no se cubre con pruebas
  automáticas: se verifica a mano, dictando.

## Lo que no entra

Reconocimiento por usuario, autenticación, y elegir motor desde configuración. El
navegador queda como el único camino de voz.
