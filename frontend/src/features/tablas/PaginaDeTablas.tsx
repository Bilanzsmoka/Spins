import { useEffect, useRef, useState } from 'react'
import { useCatalogo } from '../../core/hooks/useCatalogo'
import type { FraseSinEntender } from '../../core/hooks/useEventosDeVoz'
import type {
  ConsultaRegistrada, EventoDeVoz, ParteDeMix, SpotCompleto,
  FichaDeMemoria as FichaModelo,
} from '../../core/models/catalogo.model'
import {
  editarCelda, fijarContextoDeVoz, guardarTip, obtenerFicha, obtenerSpot,
} from '../../core/services/tablasApi'
import { EditorDeCelda } from './EditorDeCelda'
import { FrasesSinEntender } from './FrasesSinEntender'
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
  /** Lo que el intérprete rechazó, para poder enseñárselo sin salir de acá. */
  sinEntender: FraseSinEntender[]
  onOlvidarFrase: (texto: string) => void
  /** El copiloto solo se enciende desde acá: es un control del entrenamiento. */
  voz: PropsDeVoz
}

/** Qué evento de voz sembró la ficha, y para qué combinación exacta. */
interface MarcaDeFichaPorVoz {
  situacion: string
  stack: string
  spot: string
  mano: string
}

export function PaginaDeTablas({
  ultimo, historial, onLimpiarHistorial, sinEntender, onOlvidarFrase, voz,
}: Props) {
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
    // Tambien las ordenes de contexto: dictar "doce blinds" cambia la tabla
    // sin resolver mano, y la pantalla tiene que acompanar. Mirando solo
    // `resuelta` se quedaba quieta y parecia que el dictado no habia entrado.
    if (!ultimo || ultimo.tipo === 'Ignorado') return
    // oxlint-disable-next-line set-state-in-effect
    if (ultimo.claveDeStack) setStack(ultimo.claveDeStack)
    if (ultimo.spot) setSpot(ultimo.spot)
    if (ultimo.situacion) setSituacion(ultimo.situacion)
    if (ultimo.manoInterpretada) setManoAbierta(ultimo.manoInterpretada)
  }, [ultimo])

  // Al cambiar de situacion el stack activo casi nunca existe ahi: las claves
  // no se comparten entre situaciones ("10bb" en HU SB OR, "9-11bb" en BB vs
  // limp). Sin esto la pantalla pide un spot inexistente y queda en blanco.
  useEffect(() => {
    if (!catalogo || !situacion) return
    const situacionActiva = catalogo.situaciones.find((s) => s.clave === situacion)
    if (!situacionActiva) return
    if (!situacionActiva.stacks.some((t) => t.clave === stack))
      // oxlint-disable-next-line set-state-in-effect
      setStack(situacionActiva.stacks[0]?.clave ?? '')
  }, [catalogo, situacion, stack])

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

  // La tabla abierta en pantalla es la que la voz tiene que usar. Sin esto son
  // dos contextos separados: dictar una mano la resuelve contra el del
  // copiloto y el evento arrastra la pantalla hasta allá, sacándote de donde
  // estabas. Se manda el minBB del stack porque la voz razona en BB, no en
  // claves, y ese número cae dentro de la cobertura de este stack.
  useEffect(() => {
    if (!catalogo || !situacion || !stack || !spot) return
    const rango = catalogo.situaciones
      .find((s) => s.clave === situacion)?.stacks
      .find((t) => t.clave === stack)
    if (rango) void fijarContextoDeVoz(situacion, rango.minBB, spot)
  }, [catalogo, situacion, stack, spot])

  useEffect(() => {
    if (!situacion || !stack || !spot) return
    let cancelado = false
    obtenerSpot(situacion, stack, spot)
      .then((d) => { if (!cancelado) setDatos(d) })
      .catch(() => { if (!cancelado) setDatos(null) })
    return () => { cancelado = true }
  }, [situacion, stack, spot, recarga])

  // Qué combinación (situación/stack/spot/mano) sembró el último evento de
  // voz que trajo ficha, para que el efecto de abajo la use sin pedirla de
  // nuevo. Se consume una sola vez — el efecto de la mano abierta la borra
  // apenas la mira, la haya usado o no — para que sólo cuente en el render
  // inmediato siguiente a ese evento, nunca en uno posterior (tocar otra
  // celda, o `recarga` tras guardar, con la casualidad de que los campos
  // coincidan).
  const marcaDeVozRef = useRef<MarcaDeFichaPorVoz | null>(null)

  // Efecto B — la ficha que llega por voz. CopilotoDeVoz ya la manda
  // calculada en cada EventoDeCopiloto: sembrarla acá ahorra calcularla dos
  // veces y un viaje de red. Depende sólo de `ultimo`, nada más: así un
  // dictado no resuelto (o cualquier evento, resuelva lo que resuelva) no
  // toca errores ni dispara un fetch — eso es enteramente el trabajo del
  // efecto de abajo, que ni se entera de que hubo un evento de voz salvo por
  // esta marca.
  //
  // No compara contra `situacion`/`stack`/`spot`/`manoAbierta` de estado: en
  // el mismo commit en que este efecto corre, el de los selectores (arriba)
  // todavía no aplicó los campos del evento — leerlos acá los vería viejos.
  // La correspondencia real la valida el efecto de la mano abierta, un
  // render más tarde, cuando esos campos ya están puestos.
  useEffect(() => {
    if (!ultimo?.resuelta || !ultimo.ficha) return
    // oxlint-disable-next-line set-state-in-effect
    setFicha(ultimo.ficha)
    if (ultimo.situacion && ultimo.claveDeStack && ultimo.spot) {
      marcaDeVozRef.current = {
        situacion: ultimo.situacion,
        stack: ultimo.claveDeStack,
        spot: ultimo.spot,
        mano: ultimo.manoInterpretada,
      }
    }
  }, [ultimo])

  // Efecto A — la mano abierta. La ficha se pide al backend en vez de
  // derivarse de `datos`: las piezas que la arman (umbral, familias) miran
  // otros stacks y otros spots, que la pantalla no tiene cargados.
  //
  // Los errores de guardado son estado de la mano abierta, no del evento que
  // la haya abierto: mueren aca, en las dos ramas, cada vez que este efecto
  // corre — asi ningun camino de cierre (Cerrar, Escape, click en el fondo, o
  // abrir otra mano) tiene que acordarse de limpiarlos por su cuenta. No
  // depende de `ultimo`: un dictado que no cambia mano/stack/spot (uno sin
  // resolver, por ejemplo) no tiene por qué re-correr esto ni re-limpiar un
  // error que el usuario está leyendo.
  useEffect(() => {
    // oxlint-disable-next-line set-state-in-effect
    setErrorAlEditar(null)
    setErrorAlGuardarTip(null)

    const marca = marcaDeVozRef.current
    marcaDeVozRef.current = null // se usa (o se descarta) una sola vez

    if (!manoAbierta || !situacion || !stack || !spot) {
      setFicha(null)
      return
    }

    // El Efecto B ya sembró esta misma ficha un render antes: pedirla nos
    // volvería a pegar exactamente el doble fetch que el Arreglo 3 vino a
    // evitar.
    if (
      marca
      && marca.situacion === situacion
      && marca.stack === stack
      && marca.spot === spot
      && marca.mano === manoAbierta
    ) return

    let cancelado = false
    obtenerFicha(situacion, stack, spot, manoAbierta)
      .then((f) => { if (!cancelado) setFicha(f) })
      .catch(() => { if (!cancelado) setFicha(null) })
    return () => { cancelado = true }
  }, [situacion, stack, spot, manoAbierta, recarga])

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
        <div className={`respuesta-actual${ultimo.tipo === 'Ignorado' ? ' respuesta-actual-fallo' : ''}`}>
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
              {ultimo.paloAsumido && (
                <span className="respuesta-asumido" title="No se oyó el palo, así que se asumió offsuit">
                  ⚠ palo asumido
                </span>
              )}
            </>
          ) : ultimo.tipo === 'Contexto' ? (
            // Entendida y sin mano: se dice qué cambió, no que falló.
            <span className="respuesta-detalle">Listo · {ultimo.respuesta}</span>
          ) : (
            <span className="respuesta-detalle">No entendí · «{ultimo.textoCrudo}»</span>
          )}
        </div>
      )}

      {/* Va pegado a la respuesta: el rechazo se acaba de oír, y el arreglo
          es enseñarle la palabra ahí mismo. */}
      <FrasesSinEntender
        frases={sinEntender}
        situaciones={catalogo.situaciones}
        situacion={situacion}
        spot={spot}
        onOlvidar={onOlvidarFrase}
      />

      <AvisoDeProblemas problemas={catalogo.problemas} />

      <Selectores
        situaciones={catalogo.situaciones}
        situacion={situacion}
        stack={stack}
        spot={spot}
        onFormato={(formato) => {
          // El formato no se guarda: se elige moviendo la situacion a la
          // primera de ese formato, y los efectos acomodan stack y spot.
          const primera = catalogo.situaciones.find((s) => s.formato === formato)
          if (primera) setSituacion(primera.clave)
        }}
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
