import { useContext, useEffect, useState } from 'react'
import { useCatalogo } from '../../core/hooks/useCatalogo'
import type { RendimientoTotal } from '../../core/models/catalogo.model'
import { rendimiento } from '../../core/services/entrenadorApi'
import { IrAlModulo } from '../../shared/IrAlModulo'
import type { FocoDeEntrenamiento } from './foco'

interface Props {
  /** Deja anotado qué spot entrenar, para que el entrenador arranque ahí. */
  onEntrenar: (foco: FocoDeEntrenamiento) => void
}

const tiempo = (ms: number) =>
  ms <= 0 ? '' : ms < 1000 ? `${ms} ms` : `${(ms / 1000).toFixed(1).replace('.', ',')} s`

/** Verde, ámbar o rojo: el mismo semáforo que el resto de la app. */
const colorDe = (porcentaje: number) =>
  porcentaje >= 90 ? '#22c55e' : porcentaje >= 70 ? '#f59e0b' : '#ef4444'

/**
 * Cómo venís, y contra qué conviene sentarse hoy.
 *
 * La lista no es "lo que fallaste": es **por spot**, ordenada de peor a mejor.
 * Una mano suelta que erraste no dice nada; un spot con 60% sobre cuarenta
 * manos es una tabla que no sabés, y eso sí se puede ir a entrenar. Por eso
 * cada renglón tiene su botón: mirar una estadística que no te lleva a
 * arreglarla es mirar el problema, no resolverlo.
 */
export function PaginaDeRendimiento({ onEntrenar }: Props) {
  const irA = useContext(IrAlModulo)
  const { catalogo } = useCatalogo()
  const [datos, setDatos] = useState<RendimientoTotal | null>(null)
  const [error, setError] = useState<string | null>(null)

  useEffect(() => {
    let cancelado = false
    rendimiento()
      .then((r) => { if (!cancelado) { setDatos(r); setError(null) } })
      .catch((e: unknown) => {
        if (!cancelado) setError(e instanceof Error ? e.message : 'No pude leer tu rendimiento')
      })
    return () => { cancelado = true }
  }, [])

  const nombreDe = (clave: string) =>
    catalogo?.situaciones.find((s) => s.clave === clave)?.etiqueta ?? clave

  const etiquetaDelSpot = (situacion: string, stack: string, spot: string) =>
    catalogo?.situaciones.find((s) => s.clave === situacion)
      ?.stacks.find((t) => t.clave === stack)
      ?.spots.find((p) => p.clave === spot)?.etiqueta ?? spot

  const entrenar = (situacion: string, spot: string) => {
    onEntrenar({ situacion, spot })
    irA('entrenador')
  }

  return (
    <div className="rendimiento">
      <header className="entrenamiento-cabecera">
        <div>
          <h1>Cómo venís</h1>
          <p className="subtitulo">Y contra qué conviene sentarse hoy</p>
        </div>
      </header>

      {error && <p className="error">{error}</p>}

      {datos && datos.respondidas === 0 && (
        <p className="cargando">
          Todavía no contestaste ninguna mano. Entrená una tanda y esta pantalla
          empieza a tener algo que decirte.
        </p>
      )}

      {datos && datos.respondidas > 0 && (
        <>
          <div className="cifras">
            <div className="cifra">
              <b>{datos.respondidas}</b><span>manos jugadas</span>
            </div>
            <div className="cifra">
              <b style={{ color: colorDe(datos.porcentaje) }}>{datos.porcentaje}%</b>
              <span>de aciertos</span>
            </div>
            <div className="cifra">
              <b>{datos.aciertos}</b><span>bien contestadas</span>
            </div>
            <div className="cifra">
              <b>{tiempo(datos.milisegundosPromedio) || '—'}</b>
              <span>por mano</span>
            </div>
          </div>

          <section className="glosario-grupo">
            <h2>Los que peor te salen</h2>
            <p className="glosario-nota">
              Por spot, de peor a mejor. Sólo entran los que tienen al menos
              cinco respuestas: con dos, un fallo da 50% y no significa nada.
            </p>

            {datos.peoresSpots.length === 0 ? (
              <p className="cargando">
                Ningún spot llegó a cinco respuestas todavía. Seguí entrenando.
              </p>
            ) : (
              <ul className="peores">
                {datos.peoresSpots.map((s) => (
                  <li key={`${s.situacion}|${s.claveDeStack}|${s.spot}`}>
                    <span
                      className="peor-porcentaje"
                      style={{ color: colorDe(s.porcentaje) }}
                    >
                      {s.porcentaje}%
                    </span>
                    <span className="peor-donde">
                      <strong>{etiquetaDelSpot(s.situacion, s.claveDeStack, s.spot)}</strong>
                      <em>{nombreDe(s.situacion)} · {s.claveDeStack}</em>
                    </span>
                    <span className="peor-cuentas">
                      {s.aciertos} de {s.respondidas}
                      {s.milisegundosPromedio > 0 && ` · ${tiempo(s.milisegundosPromedio)}`}
                    </span>
                    {/*
                      Secundario, no primario: diez botones rellenos en fila
                      dejan de destacar y sólo hacen ruido. El relleno se
                      reserva para la acción principal de una pantalla.
                    */}
                    <button
                      type="button"
                      className="boton-repaso"
                      onClick={() => entrenar(s.situacion, s.spot)}
                    >
                      Entrenar
                    </button>
                  </li>
                ))}
              </ul>
            )}
          </section>
        </>
      )}
    </div>
  )
}
