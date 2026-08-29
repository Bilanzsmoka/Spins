import { useVozQueLee } from './useVozQueLee'

interface Props {
  termino: string
  explicacion: string
}

/**
 * Un término con su explicación y un play que lee las dos cosas.
 *
 * Lee el renglón entero y no sólo la palabra: la idea es poder estudiar sin
 * mirar la pantalla, igual que el resto de la app. Oír "limp" suelto no
 * enseña nada; oír "limp: entrar pagando exactamente la ciega grande, sin
 * subir" sí.
 */
export function TerminoConVoz({ termino, explicacion }: Props) {
  const { hablando, decir } = useVozQueLee(`${termino}. ${explicacion}`)

  return (
    <li className="glosario-termino">
      <button
        type="button"
        className={`boton-play${hablando ? ' boton-play-activo' : ''}`}
        onClick={decir}
        aria-label={hablando ? `Callar ${termino}` : `Escuchar ${termino}`}
      >
        {hablando ? '■' : '▶'}
      </button>
      <div>
        <strong className="glosario-palabra">{termino}</strong>
        <p className="glosario-explicacion">{explicacion}</p>
      </div>
    </li>
  )
}
