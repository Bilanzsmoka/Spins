import type {
  MesaDeSituacion, PreguntaDeTanda, RivalEnLaMesa, SituacionResumen, TerminoDelGlosario,
} from '../../core/models/catalogo.model'

interface Props {
  pregunta: PreguntaDeTanda
  /** La situación del catálogo: de ahí salen la mesa y la banda de stack. */
  situacion: SituacionResumen | null
  /** Los perfiles del glosario, para pintar a cada rival de su color. */
  perfiles: TerminoDelGlosario[]
  /** Cuánto llevás pensando esta mano. Cero cuando el reloj está apagado. */
  milisegundos: number
}

/**
 * Los símbolos de los dos palos que una casilla puede representar.
 *
 * Los glifos son literales a propósito: no hay registro del que puedan salir,
 * y ♠ no cambia. Las claves `s` y `o` son las de `vocabulario.palos`.
 */
const PALOS = { s: '♠', o: '♦' } as const

/** A partir de acá el reloj avisa. Son señales, no reglas: nada se reprueba. */
const PENSANDO_DEMASIADO_MS = 5000
const MUY_LENTO_MS = 10000

/**
 * Dónde se sienta cada uno. Vos siempre abajo; un rival solo va arriba al
 * medio y dos se reparten a los costados, que es como se ve un heads-up y un
 * 3-max de verdad.
 *
 * Las clases llevan la geometría; acá sólo se decide qué silla le toca a quién,
 * por el orden en que el archivo los declara.
 */
const SILLAS = { uno: ['centro'], dos: ['izquierda', 'derecha'] } as const

const seFue = (hizo: string) => hizo.toLowerCase() === 'fold'
const esAllIn = (hizo: string) => hizo.toLowerCase() === 'all-in'

/** "0,5" y no "0.5": es como se escribe acá y como se lee de un vistazo. */
const bb = (n: number) => String(n).replace('.', ',')

function Reloj({ milisegundos }: { milisegundos: number }) {
  const estado = milisegundos >= MUY_LENTO_MS
    ? ' mesa-reloj-lento'
    : milisegundos >= PENSANDO_DEMASIADO_MS ? ' mesa-reloj-pensando' : ''

  return (
    <span className={`mesa-reloj${estado}`}>
      {(milisegundos / 1000).toFixed(1).replace('.', ',')} s
    </span>
  )
}

/** Una carta, con el palo en la esquina y grande en el medio. */
function Carta({ rango, palo }: { rango: string; palo: string }) {
  return (
    <div className={`card${palo === PALOS.o ? ' card-roja' : ''}`}>
      <div className="corner">
        <div className="rank">{rango}</div>
        <div className="suit">{palo}</div>
      </div>
      <div className="big-suit">{palo}</div>
    </div>
  )
}

/**
 * Las fichas que alguien tiene puestas, sobre el paño y del lado de su silla.
 * El botón viaja con ellas: es la marca de esa misma silla.
 */
function Fichas({
  puso, hizo, silla, conBoton,
}: { puso: number | null; hizo: string; silla: string; conBoton: boolean }) {
  const todo = esAllIn(hizo)
  if (!todo && !puso && !conBoton) return null

  return (
    <div className={`blind blind-${silla}`}>
      {(todo || puso) && <span className="chip" />}
      {todo
        ? <span className="blind-label blind-todo">ALL-IN</span>
        : puso ? <span className="blind-label">{bb(puso)} BB</span> : null}
      {conBoton && <span className="dealer">D</span>}
    </div>
  )
}

function Jugador({
  rival, silla, banda, perfil,
}: {
  rival: RivalEnLaMesa
  silla: string
  banda: string
  perfil?: TerminoDelGlosario
}) {
  const fuera = seFue(rival.hizo)

  return (
    <div className={`player player-${silla}${fuera ? ' player-fuera' : ''}`}>
      <div className="pos">{rival.posicion}</div>
      {/*
        La figura del tipo de jugador va DENTRO de la carátula, no al lado: es
        lo que mirás para decidir, y a un costado se pierde. La silueta gris
        queda para el rival del que no sabemos el tipo. El que se fue va
        apagado y sin color: ya no hay a quién leerle nada.
      */}
      <div
        className={`avatar${perfil?.icono ? ' avatar-con-figura' : ''}`}
        style={perfil?.color && !fuera
          ? { borderColor: perfil.color, background: perfil.color, color: perfil.colorTexto }
          : undefined}
        title={rival.tipo}
      >
        {perfil?.icono}
      </div>
      <div className="player-info">
        {/*
          La clase sale del dato, no de una lista en código: un estado nuevo en
          el JSON estrena su color agregando una regla de CSS, y si no la tiene
          cae al estilo neutro en vez de romperse. Que all-in, subida y fold se
          vean distinto es lo que hace leer la mesa sin leer el texto.
        */}
        <div className={`action accion-${rival.hizo.toLowerCase().replace(/\s+/g, '-')}`}>
          {rival.hizo.toUpperCase()}
        </div>
        <div className="stack">{banda}</div>
      </div>
    </div>
  )
}

/**
 * La mesa como la ves cuando te toca decidir.
 *
 * El diseño es el que pidió el usuario, con sus medidas y sus colores; lo que
 * agrega el código es que se maneje sola con los datos: quién se sienta dónde,
 * quién tiene el botón, qué puso cada uno y de qué tipo es, todo del bloque
 * `mesa` de cada tabla.
 *
 * **No dice la mano.** En una mesa nunca ves «AKo»: ves dos cartas y las tenés
 * que leer. Mostrar la etiqueta convertía el ejercicio en reconocer un código,
 * y eso no se transfiere al juego.
 */
export function MesaSimulada({ pregunta, situacion, perfiles, milisegundos }: Props) {
  const mesa: MesaDeSituacion | null = situacion?.mesa ?? null
  const stack = situacion?.stacks.find((s) => s.clave === pregunta.claveDeStack) ?? null
  const banda = stack
    ? (stack.minBB === stack.maxBB ? `${stack.minBB} BB` : `${stack.minBB}-${stack.maxBB} BB`)
    : ''

  const [alto, bajo] = [pregunta.mano[0], pregunta.mano[1]]
  const palo = pregunta.mano.length > 2 ? pregunta.mano[2] : null
  const perfil = (tipo: string) =>
    perfiles.find((p) => p.termino.toLowerCase() === tipo.toLowerCase())

  const rivales = mesa?.rivales ?? []
  const sillas = rivales.length === 1 ? SILLAS.uno : SILLAS.dos

  return (
    <section className="mesa">
      <p className="mesa-donde">
        {pregunta.etiquetaDeSpot}
        {pregunta.esNueva && <span className="mesa-nueva">nueva</span>}
        {milisegundos > 0 && <Reloj milisegundos={milisegundos} />}
      </p>

      <div className="table-area">
        <div className="table">
          <div className="logo">{situacion?.formato ?? ''}</div>

          {rivales.map((rival, i) => (
            <Fichas
              key={rival.posicion}
              puso={rival.puso}
              hizo={rival.hizo}
              silla={sillas[i] ?? 'centro'}
              conBoton={rival.posicion === mesa?.boton}
            />
          ))}

          {mesa && (
            <Fichas
              puso={mesa.pusoElHeroe}
              hizo=""
              silla="abajo"
              conBoton={mesa.heroe === mesa.boton}
            />
          )}
        </div>

        {rivales.map((rival, i) => (
          <Jugador
            key={rival.posicion}
            rival={rival}
            silla={sillas[i] ?? 'centro'}
            banda={banda}
            perfil={perfil(rival.tipo)}
          />
        ))}

        <div className="player player-heroe">
          {mesa && <div className="pos">{mesa.heroe}</div>}

          <div className="cards">
            <Carta rango={alto} palo={PALOS.s} />
            <Carta rango={bajo} palo={palo === 's' ? PALOS.s : PALOS.o} />
          </div>

          <div className="avatar hero-avatar" />
          <div className="hero-stack">{banda}</div>
        </div>
      </div>
    </section>
  )
}
