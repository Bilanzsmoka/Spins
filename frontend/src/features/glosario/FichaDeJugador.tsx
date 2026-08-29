import type { TerminoDelGlosario } from '../../core/models/catalogo.model'
import { useVozQueLee } from './useVozQueLee'

interface Props {
  jugador: TerminoDelGlosario
}

/**
 * Un perfil de rival: círculo de color con su figura, el nombre, y las dos o
 * tres señales que lo delatan en la mesa.
 *
 * Entra por el ojo antes que por la lectura, y eso es a propósito: en una mesa
 * no hay tiempo de leer un párrafo, hay tiempo de acordarse de que el naranja
 * es el que siempre sube. El color y la figura salen del JSON —no del
 * código— justamente porque son los que después van a etiquetar rivales de
 * verdad.
 */
export function FichaDeJugador({ jugador }: Props) {
  const { termino, perfil, explicacion, color, colorTexto, icono, rasgos } = jugador
  const { hablando, decir } = useVozQueLee(
    [termino, perfil, explicacion].filter(Boolean).join('. '),
  )

  return (
    <li className="jugador-ficha" style={color ? { borderColor: color } : undefined}>
      <div
        className="jugador-circulo"
        style={{ background: color ?? 'var(--desconocido)', color: colorTexto ?? 'var(--texto)' }}
        aria-hidden="true"
      >
        {icono ?? termino.slice(0, 1)}
      </div>

      <div className="jugador-cuerpo">
        <div className="jugador-titulo">
          <button
            type="button"
            className={`boton-play${hablando ? ' boton-play-activo' : ''}`}
            onClick={decir}
            aria-label={hablando ? `Callar ${termino}` : `Escuchar ${termino}`}
          >
            {hablando ? '■' : '▶'}
          </button>
          <strong className="glosario-palabra">{termino}</strong>
          {perfil && (
            <span
              className="jugador-perfil"
              style={color ? { background: color, color: colorTexto } : undefined}
            >
              {perfil}
            </span>
          )}
        </div>

        {rasgos && rasgos.length > 0 && (
          <ul className="jugador-rasgos">
            {rasgos.map((rasgo) => (
              <li key={rasgo}>{rasgo}</li>
            ))}
          </ul>
        )}

        <p className="glosario-explicacion">{explicacion}</p>
      </div>
    </li>
  )
}
