import type {
  MesaDeSituacion, PreguntaDeTanda, SituacionResumen, TerminoDelGlosario,
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

/** Un rival que ya no está en la mano se dibuja apagado, no se esconde. */
const seFue = (hizo: string) => hizo.toLowerCase() === 'fold'

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

/**
 * La mesa como la ves cuando te toca decidir: quién está sentado dónde, qué
 * hizo cada uno, tu stack y tus dos cartas.
 *
 * Dos decisiones que la hacen servir para lo que existe:
 *
 * **No dice la mano.** En una mesa nunca ves «AKo»: ves dos cartas y las tenés
 * que leer. Mostrar la etiqueta convertía el ejercicio en reconocer un código
 * en vez de leer una mano, y eso no se transfiere al juego.
 *
 * **Todo lo que dibuja está declarado.** Las posiciones, los tipos de rival y
 * lo que hicieron salen del bloque `mesa` de cada tabla; el color de cada
 * rival, del glosario. Nada se deduce de la clave de la situación: una mesa
 * mal dibujada enseñaría una mano equivocada.
 */
export function MesaSimulada({ pregunta, situacion, perfiles, milisegundos }: Props) {
  const mesa: MesaDeSituacion | null = situacion?.mesa ?? null
  const banda = situacion?.stacks.find((s) => s.clave === pregunta.claveDeStack) ?? null

  const [alto, bajo] = [pregunta.mano[0], pregunta.mano[1]]
  const palo = pregunta.mano.length > 2 ? pregunta.mano[2] : null
  const color = (tipo: string) =>
    perfiles.find((p) => p.termino.toLowerCase() === tipo.toLowerCase())

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
            {mesa.rivales.map((rival) => {
              const perfil = color(rival.tipo)
              return (
                <div
                  key={rival.posicion}
                  className={`mesa-silla${seFue(rival.hizo) ? ' mesa-silla-fuera' : ''}`}
                >
                  <span
                    className="mesa-ficha"
                    style={perfil?.color && !seFue(rival.hizo)
                      ? { background: perfil.color, color: perfil.colorTexto }
                      : undefined}
                    title={rival.tipo}
                  >
                    {perfil?.icono ?? rival.posicion.slice(0, 1)}
                  </span>
                  <strong>{rival.posicion}</strong>
                  <span className="mesa-hizo">{rival.hizo}</span>
                </div>
              )
            })}
          </div>
        )}

        <div className="mesa-centro">
          {banda && (
            <span className="mesa-stack">
              {banda.minBB === banda.maxBB
                ? `${banda.minBB} BB`
                : `${banda.minBB}-${banda.maxBB} BB`}
            </span>
          )}
          {mesa && (
            <span className="mesa-ciegas">
              ciegas {String(mesa.ciegaChica).replace('.', ',')} / {mesa.ciegaGrande}
            </span>
          )}
        </div>

        <div className="mesa-heroe">
          {mesa && <strong className="mesa-posicion">{mesa.heroe}</strong>}
          <div className="mesa-cartas">
            <span className="carta carta-negra">
              <strong>{alto}</strong><em>{PALOS.s}</em>
            </span>
            <span className={`carta ${palo === 's' ? 'carta-negra' : 'carta-roja'}`}>
              <strong>{bajo}</strong><em>{palo === 's' ? PALOS.s : PALOS.o}</em>
            </span>
          </div>
        </div>
      </div>
    </section>
  )
}
