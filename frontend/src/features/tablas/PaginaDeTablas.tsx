import { useEffect, useRef, useState } from 'react'
import { useCatalogo } from '../../core/hooks/useCatalogo'
import type {
  ConsultaRegistrada, EventoDeVoz, ParteDeMix, SpotCompleto,
  FichaDeMemoria as FichaModelo,
} from '../../core/models/catalogo.model'
import { editarCelda, guardarTip, obtenerFicha, obtenerSpot } from '../../core/services/tablasApi'
import { EditorDeCelda } from './EditorDeCelda'
import { FichaDeMemoria } from './FichaDeMemoria'
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
  const [editando, setEditando] = useState(false)
  const [manoAbierta, setManoAbierta] = useState<string | null>(null)
  const [ficha, setFicha] = useState<FichaModelo | null>(null)
  const [guardandoTip, setGuardandoTip] = useState(false)
  const [errorAlGuardarTip, setErrorAlGuardarTip] = useState<string | null>(null)
  const [guardando, setGuardando] = useState(false)
  const [errorAlEditar, setErrorAlEditar] = useState<string | null>(null)
  const [recarga, setRecarga] = useState(0)

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
    if (ultimo.manoInterpretada) setManoAbierta(ultimo.manoInterpretada)
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
  }, [situacion, stack, spot, recarga])

  // El evento de voz ya trae la ficha calculada (CopilotoDeVoz la manda en
  // el SSE): si lo que hay en pantalla es justo lo que ese evento resolvió,
  // usarla ahorra calcularla dos veces y un viaje de red. Se sirve a lo sumo
  // una vez por evento — la referencia guardada acá es la marca de "ya la
  // usé" — para que un efecto que corre por otra razón (tocar una celda a
  // mano, o `recarga` tras guardar un tip o una celda) no siga sirviendo una
  // ficha vieja sólo porque los campos coinciden por casualidad.
  const fichaDeVozUsadaRef = useRef<EventoDeVoz | null>(null)

  // La ficha se pide al backend en vez de derivarse de `datos`: las piezas que
  // la arman (umbral, familias) miran otros stacks y otros spots, que la
  // pantalla no tiene cargados.
  //
  // Los errores de guardado son estado de la mano abierta, no del boton que se
  // toco para cerrar: mueren aca, en las dos ramas, cada vez que este efecto
  // corre — asi ningun camino de cierre (Cerrar, Escape, click en el fondo, o
  // abrir otra mano) tiene que acordarse de limpiarlos por su cuenta.
  useEffect(() => {
    // oxlint-disable-next-line set-state-in-effect
    setErrorAlEditar(null)
    setErrorAlGuardarTip(null)
    if (!manoAbierta || !situacion || !stack || !spot) {
      setFicha(null)
      return
    }

    if (
      ultimo?.ficha
      && ultimo !== fichaDeVozUsadaRef.current
      && ultimo.situacion === situacion
      && ultimo.claveDeStack === stack
      && ultimo.spot === spot
      && ultimo.manoInterpretada === manoAbierta
    ) {
      fichaDeVozUsadaRef.current = ultimo
      setFicha(ultimo.ficha)
      return
    }

    let cancelado = false
    obtenerFicha(situacion, stack, spot, manoAbierta)
      .then((f) => { if (!cancelado) setFicha(f) })
      .catch(() => { if (!cancelado) setFicha(null) })
    return () => { cancelado = true }
  }, [situacion, stack, spot, manoAbierta, recarga, ultimo])

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
          <button
            type="button"
            className={`boton-repaso${editando ? ' boton-editando' : ''}`}
            onClick={() => setEditando(!editando)}
          >
            {editando ? 'Terminar edicion' : 'Corregir tabla'}
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
                onTocarCelda={setManoAbierta}
              />
              <Leyenda acciones={catalogo.acciones} spot={datos} />
            </>
          )}
        </div>

        <Sugerencias
          historial={historial}
          acciones={catalogo.acciones}
          onLimpiar={onLimpiarHistorial}
        />
      </div>

      {ficha && (
        <FichaDeMemoria
          ficha={ficha}
          acciones={catalogo.acciones}
          guardandoTip={guardandoTip}
          errorAlGuardarTip={errorAlGuardarTip}
          onCerrar={() => { setManoAbierta(null); setErrorAlGuardarTip(null) }}
          onGuardarTip={(texto) => {
            setGuardandoTip(true)
            setErrorAlGuardarTip(null)
            guardarTip(situacion, stack, spot, texto)
              .then(() => setRecarga((n) => n + 1))
              .catch((e: unknown) =>
                setErrorAlGuardarTip(e instanceof Error ? e.message : 'No pude guardar el tip'))
              .finally(() => setGuardandoTip(false))
          }}
        >
          {editando && (() => {
            const celda = datos?.celdas.find((c) => c.mano === ficha.mano)
            if (!celda) return null
            return (
              <>
                {errorAlEditar && <p className="error">{errorAlEditar}</p>}
                <EditorDeCelda
                  celda={celda}
                  acciones={catalogo.acciones}
                  guardando={guardando}
                  onGuardar={(accion: string | null, mix: ParteDeMix[] | null) => {
                    setGuardando(true)
                    setErrorAlEditar(null)
                    editarCelda(situacion, stack, spot, ficha.mano, { accion, mix })
                      .then(() => setRecarga((n) => n + 1))
                      .catch((e: unknown) =>
                        setErrorAlEditar(e instanceof Error ? e.message : 'No pude guardar'))
                      .finally(() => setGuardando(false))
                  }}
                />
              </>
            )
          })()}
        </FichaDeMemoria>
      )}
    </div>
  )
}
