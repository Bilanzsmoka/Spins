import { useCallback, useEffect, useMemo, useState } from 'react'
import type { AccionDefinida, Catalogo, SpotCompleto } from '../../core/models/catalogo.model'
import { obtenerSpot } from '../../core/services/tablasApi'
import { Grilla } from './Grilla'
import { Leyenda } from './Leyenda'

interface Paso {
  situacion: string
  stack: string
  spot: string
  etiquetaSpot: string
}

interface Props {
  catalogo: Catalogo
  acciones: AccionDefinida[]
  onSalir: () => void
}

/** Las tablas vistas hoy, para no perder el avance al recargar. */
const claveDeHoy = () => `repaso-${new Date().toLocaleDateString('sv')}`

function leerVistas(): Set<string> {
  try {
    const guardado = localStorage.getItem(claveDeHoy())
    return new Set(guardado ? (JSON.parse(guardado) as string[]) : [])
  } catch {
    return new Set()
  }
}

/**
 * El calentamiento: recorrer todas las tablas antes de jugar, con el teclado.
 * El avance se guarda por día en el navegador, así que arranca de cero cada
 * jornada — es una rutina previa a la sesión, no un progreso acumulado.
 */
export function RepasoDeTablas({ catalogo, acciones, onSalir }: Props) {
  const pasos = useMemo<Paso[]>(
    () => catalogo.situaciones.flatMap((situacion) =>
      situacion.stacks.flatMap((stack) =>
        stack.spots.map((spot) => ({
          situacion: situacion.clave,
          stack: stack.clave,
          spot: spot.clave,
          etiquetaSpot: spot.etiqueta,
        })))),
    [catalogo])

  const identidadDe = (paso: Paso) => `${paso.stack}/${paso.spot}`

  const [indice, setIndice] = useState(0)
  // El dato lleva la clave del spot al que pertenece: asi se deriva en el
  // render si corresponde al paso actual, en vez de limpiarlo con setState.
  const [cargado, setCargado] = useState<{ clave: string; datos: SpotCompleto } | null>(null)
  const [vistas, setVistas] = useState<Set<string>>(() => {
    const previas = leerVistas()
    // La primera se ve al abrir el repaso.
    if (pasos[0]) previas.add(identidadDe(pasos[0]))
    return previas
  })

  const paso = pasos[indice]
  const identidad = paso ? identidadDe(paso) : ''
  const datos = cargado?.clave === identidad ? cargado.datos : null

  useEffect(() => {
    if (!paso) return
    let cancelado = false
    obtenerSpot(paso.situacion, paso.stack, paso.spot)
      .then((d) => { if (!cancelado) setCargado({ clave: identidadDe(paso), datos: d }) })
      .catch(() => { /* se queda mostrando "cargando" */ })
    return () => { cancelado = true }
  }, [paso])

  // Persistir es sincronizar con un sistema externo; no toca estado de React.
  useEffect(() => {
    try { localStorage.setItem(claveDeHoy(), JSON.stringify([...vistas])) } catch { /* modo privado */ }
  }, [vistas])

  // Se marca vista en el evento que la muestra, no en un efecto.
  const mover = useCallback((delta: number) => {
    setIndice((previo) => {
      const siguiente = Math.min(Math.max(previo + delta, 0), pasos.length - 1)
      const destino = pasos[siguiente]
      if (destino) setVistas((antes) => antes.has(identidadDe(destino))
        ? antes
        : new Set(antes).add(identidadDe(destino)))
      return siguiente
    })
  }, [pasos])

  useEffect(() => {
    const alTeclear = (evento: KeyboardEvent) => {
      if (evento.key === 'ArrowRight' || evento.key === ' ') { evento.preventDefault(); mover(1) }
      else if (evento.key === 'ArrowLeft') { evento.preventDefault(); mover(-1) }
      else if (evento.key === 'Escape') onSalir()
    }
    window.addEventListener('keydown', alTeclear)
    return () => window.removeEventListener('keydown', alTeclear)
  }, [mover, onSalir])

  if (!paso) return null

  const vistasHoy = pasos.filter((p) => vistas.has(`${p.stack}/${p.spot}`)).length
  const porcentaje = Math.round((vistasHoy / pasos.length) * 100)
  const ultima = indice === pasos.length - 1

  return (
    <div className="repaso">
      <header className="repaso-cabecera">
        <div>
          <p className="eyebrow">Repaso previo a la sesión</p>
          <h1>{paso.stack} · {paso.etiquetaSpot}</h1>
        </div>
        <button type="button" className="boton-tenue" onClick={onSalir}>
          Salir · <kbd>Esc</kbd>
        </button>
      </header>

      <div className="repaso-avance">
        <div className="barra"><i className="barra-bien" style={{ width: `${porcentaje}%` }} /></div>
        <span className="repaso-cifra">
          {indice + 1} de {pasos.length} · {vistasHoy} vistas hoy
        </span>
      </div>

      {datos ? (
        <>
          <Grilla spot={datos} acciones={acciones} manoResaltada={null} />
          <Leyenda acciones={acciones} spot={datos} />
        </>
      ) : (
        <p className="cargando">Cargando…</p>
      )}

      <nav className="repaso-controles">
        <button type="button" className="boton-tenue" disabled={indice === 0} onClick={() => mover(-1)}>
          <kbd>←</kbd> Anterior
        </button>
        {ultima ? (
          <button type="button" className="boton-principal" onClick={onSalir}>
            Terminar repaso
          </button>
        ) : (
          <button type="button" className="boton-principal" onClick={() => mover(1)}>
            Siguiente · <kbd>→</kbd>
          </button>
        )}
      </nav>

      <p className="repaso-nota">
        Espacio o <kbd>→</kbd> para avanzar. El avance se guarda por día: si
        salís y volvés, seguís donde ibas.
      </p>
    </div>
  )
}
