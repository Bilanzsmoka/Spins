import { useEffect, useState } from 'react'
import { useCatalogo } from '../../core/hooks/useCatalogo'
import type { ConsultaRegistrada, EventoDeVoz, SpotCompleto } from '../../core/models/catalogo.model'
import { obtenerSpot } from '../../core/services/tablasApi'
import { AvisoDeProblemas } from './AvisoDeProblemas'
import { ControlDeVoz, type PropsDeVoz } from './ControlDeVoz'
import { Grilla } from './Grilla'
import { Leyenda } from './Leyenda'
import { RepasoDeTablas } from './RepasoDeTablas'
import { Selectores } from './Selectores'
import { Sugerencias } from './Sugerencias'

interface Props {
  ultimo: EventoDeVoz | null
  historial: ConsultaRegistrada[]
  onLimpiarHistorial: () => void
  /** El copiloto solo se enciende desde acá: es un control del entrenamiento. */
  voz: PropsDeVoz
}

export function PaginaDeTablas({ ultimo, historial, onLimpiarHistorial, voz }: Props) {
  const { catalogo, error } = useCatalogo()

  const [situacion, setSituacion] = useState('')
  const [stack, setStack] = useState('')
  const [spot, setSpot] = useState('')
  const [datos, setDatos] = useState<SpotCompleto | null>(null)
  const [repasando, setRepasando] = useState(false)

  // Seleccion inicial: la primera de cada nivel, tomada del catalogo.
  // Sincroniza con un sistema externo (el fetch del catalogo), que es
  // exactamente el caso que useEffect existe para cubrir.
  useEffect(() => {
    if (!catalogo || situacion) return
    const primera = catalogo.situaciones[0]
    if (!primera) return
    const primerStack = primera.stacks[0]
    // oxlint-disable-next-line set-state-in-effect
    setSituacion(primera.clave)
    setStack(primerStack?.clave ?? '')
    setSpot(primerStack?.spots[0]?.clave ?? '')
  }, [catalogo, situacion])

  // La voz manda sobre los selectores: si el dictado trajo stack o spot,
  // la pantalla se mueve a la tabla que se acaba de consultar.
  useEffect(() => {
    if (!ultimo?.resuelta) return
    // oxlint-disable-next-line set-state-in-effect
    if (ultimo.claveDeStack) setStack(ultimo.claveDeStack)
    if (ultimo.spot) setSpot(ultimo.spot)
    if (ultimo.situacion) setSituacion(ultimo.situacion)
  }, [ultimo])

  // Al cambiar de stack, el spot activo puede no existir ahi (los stacks
  // chicos tienen 3 spots y los demas 5). Caer al primero disponible.
  useEffect(() => {
    if (!catalogo || !situacion || !stack) return
    const stackActivo = catalogo.situaciones
      .find((s) => s.clave === situacion)?.stacks
      .find((t) => t.clave === stack)
    if (stackActivo && !stackActivo.spots.some((p) => p.clave === spot))
      // oxlint-disable-next-line set-state-in-effect
      setSpot(stackActivo.spots[0]?.clave ?? '')
  }, [catalogo, situacion, stack, spot])

  useEffect(() => {
    if (!situacion || !stack || !spot) return
    let cancelado = false
    obtenerSpot(situacion, stack, spot)
      .then((d) => { if (!cancelado) setDatos(d) })
      .catch(() => { if (!cancelado) setDatos(null) })
    return () => { cancelado = true }
  }, [situacion, stack, spot])

  if (error) return <p className="error">No pude cargar el catálogo: {error}</p>
  if (!catalogo) return <p className="cargando">Cargando…</p>

  if (repasando) return (
    <RepasoDeTablas
      catalogo={catalogo}
      acciones={catalogo.acciones}
      onSalir={() => setRepasando(false)}
    />
  )

  // El evento trae el codigo de accion (ALL-IN, CALL...): con eso alcanza
  // para colorear la respuesta con el mismo color que la celda, en vez de
  // adivinarlo leyendo la frase hablada.
  const accionRespondida = catalogo.acciones.find((a) => a.clave === ultimo?.accion)

  return (
    <div className="entrenamiento">
      <header className="entrenamiento-cabecera">
        <div>
          <h1>Entrenamiento</h1>
          <p className="subtitulo">Tablas preflop · dictá una mano y te la responde</p>
        </div>
        <div className="cabecera-acciones">
          {/* El calentamiento previo a la sesion: pasar todas las tablas antes
              de abrir la sala, no consultarlas en medio de una mano. */}
          <button type="button" className="boton-repaso" onClick={() => setRepasando(true)}>
            Repasar todas
          </button>
          <ControlDeVoz {...voz} />
        </div>
      </header>

      {/* La ultima respuesta, grande. Lo hablado se pierde; esto queda a la
          vista mientras se juega la mano. */}
      {ultimo && (
        <div className={`respuesta-actual${ultimo.resuelta ? '' : ' respuesta-actual-fallo'}`}>
          {ultimo.resuelta ? (
            <>
              <span className="respuesta-mano">{ultimo.manoInterpretada}</span>
              <span
                className="respuesta-accion"
                style={accionRespondida
                  ? { background: accionRespondida.color, color: accionRespondida.colorTexto }
                  : undefined}
              >
                {accionRespondida?.etiqueta ?? ultimo.accion}
              </span>
              <span className="respuesta-detalle">{ultimo.respuesta}</span>
            </>
          ) : (
            <span className="respuesta-detalle">No entendí · «{ultimo.textoCrudo}»</span>
          )}
        </div>
      )}

      <AvisoDeProblemas problemas={catalogo.problemas} />

      <Selectores
        situaciones={catalogo.situaciones}
        situacion={situacion}
        stack={stack}
        spot={spot}
        onSituacion={setSituacion}
        onStack={setStack}
        onSpot={setSpot}
      />

      <div className="entrenamiento-cuerpo">
        <div className="entrenamiento-tabla">
          {datos && (
            <>
              <Grilla
                spot={datos}
                acciones={catalogo.acciones}
                manoResaltada={ultimo?.manoInterpretada || null}
              />
              <Leyenda acciones={catalogo.acciones} conteos={datos.conteos} />
            </>
          )}
        </div>

        <Sugerencias
          historial={historial}
          acciones={catalogo.acciones}
          onLimpiar={onLimpiarHistorial}
        />
      </div>
    </div>
  )
}
