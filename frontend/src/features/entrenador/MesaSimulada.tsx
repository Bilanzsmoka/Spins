import type { PreguntaDeTanda } from '../../core/models/catalogo.model'

interface Props {
  pregunta: PreguntaDeTanda
}

/**
 * Los símbolos de los dos palos que una casilla puede representar.
 *
 * Los glifos son literales a propósito: no hay registro del que puedan salir,
 * y ♠ no cambia. Las claves `s` y `o`, en cambio, son las de
 * `vocabulario.palos` — si algún día aparece un tercer palo ahí, esto lo
 * dibuja como offsuit sin quejarse: pinta rojo en silencio.
 */
const PALOS = { s: '♠', o: '♦' } as const

/**
 * La mesa de la pregunta: las dos cartas grandes, la banda de stack y dónde
 * estás.
 *
 * Muestra lo que los datos sostienen y nada más. El spec pedía además el bote
 * y las fichas del rival, y ninguna tabla los declara: calcularlos por tipo de
 * spot sería deducirlos de la clave, que es justo lo que el proyecto no hace.
 * La etiqueta de la situación ya trae el rival ("BB vs limp | fish"), así que
 * se muestra tal cual en vez de partirla.
 *
 * Una casilla suited se dibuja con dos picas y una offsuit con pica y diamante:
 * el palo concreto no importa —la tabla razona por casilla, no por combo— pero
 * verlo en colores distintos es lo que hace leer "offsuit" de un vistazo.
 */
export function MesaSimulada({ pregunta }: Props) {
  const [alto, bajo] = [pregunta.mano[0], pregunta.mano[1]]
  const palo = pregunta.mano.length > 2 ? pregunta.mano[2] : null
  const segundoPalo = palo === 's' ? PALOS.s : PALOS.o

  return (
    <section className="mesa">
      <p className="mesa-donde">
        {pregunta.etiquetaDeSituacion} · {pregunta.claveDeStack} · {pregunta.etiquetaDeSpot}
        {pregunta.esNueva && <span className="mesa-nueva">nueva</span>}
      </p>

      <div className="mesa-cartas">
        <span className="carta carta-negra">
          <strong>{alto}</strong><em>{PALOS.s}</em>
        </span>
        <span className={`carta ${palo === 's' ? 'carta-negra' : 'carta-roja'}`}>
          <strong>{bajo}</strong><em>{segundoPalo}</em>
        </span>
      </div>

      <p className="mesa-mano">{pregunta.mano}</p>
    </section>
  )
}
