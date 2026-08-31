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

const seFue = (hizo: string) => hizo.toLowerCase() === 'fold'

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

/** Las fichas que alguien tiene puestas delante, camino al pozo. */
function Apuesta({ puso, hizo }: { puso: number | null; hizo: string }) {
  if (hizo.toLowerCase() === 'all-in') return <span className="mesa-apuesta mesa-apuesta-todo">ALL-IN</span>
  if (!puso) return null
  return <span className="mesa-apuesta">{bb(puso)}</span>
}

function Silla({
  rival, boton, perfil,
}: { rival: RivalEnLaMesa; boton: string; perfil?: TerminoDelGlosario }) {
  const fuera = seFue(rival.hizo)

  return (
    <div className={`mesa-silla${fuera ? ' mesa-silla-fuera' : ''}`}>
      <div className="mesa-jugador">
        <span
          className="mesa-ficha"
          style={perfil?.color && !fuera
            ? { background: perfil.color, color: perfil.colorTexto }
            : undefined}
          title={rival.tipo}
        >
          {perfil?.icono ?? rival.posicion.slice(0, 1)}
        </span>
        {rival.posicion === boton && <span className="mesa-boton" title="Botón">D</span>}
      </div>
      <strong>{rival.posicion}</strong>
      <span className="mesa-hizo">{rival.hizo}</span>
      <Apuesta puso={rival.puso} hizo={rival.hizo} />
    </div>
  )
}

/**
 * La mesa como la ves cuando te toca decidir: quién está sentado dónde, quién
 * tiene el botón, qué puso cada uno y tus dos cartas.
 *
 * Tres decisiones que la hacen servir para lo que existe:
 *
 * **No dice la mano.** En una mesa nunca ves «AKo»: ves dos cartas y las tenés
 * que leer. Mostrar la etiqueta convertía el ejercicio en reconocer un código.
 *
 * **Vos abajo y los rivales enfrente**, como en cualquier sala. El orden de las
 * sillas es el del archivo, no uno que deduzca el código de los nombres de las
 * posiciones.
 *
 * **Todo está declarado**: sillas, botón, tipos y fichas salen del bloque
 * `mesa` de cada tabla. Una mesa mal dibujada no rompe nada y enseña una mano
 * equivocada.
 */
export function MesaSimulada({ pregunta, situacion, perfiles, milisegundos }: Props) {
  const mesa: MesaDeSituacion | null = situacion?.mesa ?? null
  const banda = situacion?.stacks.find((s) => s.clave === pregunta.claveDeStack) ?? null

  const [alto, bajo] = [pregunta.mano[0], pregunta.mano[1]]
  const palo = pregunta.mano.length > 2 ? pregunta.mano[2] : null
  const perfil = (tipo: string) =>
    perfiles.find((p) => p.termino.toLowerCase() === tipo.toLowerCase())

  // El pozo sólo si se sabe entero: con alguien all-in falta su stack, e
  // inventar un número sería peor que no mostrarlo.
  const alguienAllIn = mesa?.rivales.some((r) => r.hizo.toLowerCase() === 'all-in') ?? false
  const pozo = mesa && !alguienAllIn
    ? mesa.rivales.reduce((suma, r) => suma + (r.puso ?? 0), 0) + mesa.pusoElHeroe
    : null

  return (
    <section className="mesa">
      <p className="mesa-donde">
        {pregunta.etiquetaDeSpot}
        {pregunta.esNueva && <span className="mesa-nueva">nueva</span>}
        {milisegundos > 0 && <Reloj milisegundos={milisegundos} />}
      </p>

      <div className="mesa-panio">
        {mesa && (
          <div className="mesa-rivales">
            {mesa.rivales.map((rival) => (
              <Silla
                key={rival.posicion}
                rival={rival}
                boton={mesa.boton}
                perfil={perfil(rival.tipo)}
              />
            ))}
          </div>
        )}

        <div className="mesa-centro">
          {pozo !== null && <span className="mesa-pozo">pozo {bb(pozo)} BB</span>}
          {mesa && (
            <span className="mesa-ciegas">
              ciegas {bb(mesa.ciegaChica)} / {bb(mesa.ciegaGrande)}
            </span>
          )}
        </div>

        <div className="mesa-heroe">
          <div className="mesa-cartas">
            <span className="carta carta-negra">
              <strong>{alto}</strong><em>{PALOS.s}</em>
            </span>
            <span className={`carta ${palo === 's' ? 'carta-negra' : 'carta-roja'}`}>
              <strong>{bajo}</strong><em>{palo === 's' ? PALOS.s : PALOS.o}</em>
            </span>
          </div>

          <div className="mesa-yo">
            {mesa && <strong className="mesa-posicion">{mesa.heroe}</strong>}
            {mesa && mesa.heroe === mesa.boton && <span className="mesa-boton" title="Botón">D</span>}
            {banda && (
              <span className="mesa-stack">
                {banda.minBB === banda.maxBB
                  ? `${banda.minBB} BB`
                  : `${banda.minBB}-${banda.maxBB} BB`}
              </span>
            )}
            {mesa && mesa.pusoElHeroe > 0 && (
              <span className="mesa-apuesta">{bb(mesa.pusoElHeroe)}</span>
            )}
          </div>
        </div>
      </div>
    </section>
  )
}
