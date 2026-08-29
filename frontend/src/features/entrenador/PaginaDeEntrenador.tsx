import { useEffect, useRef, useState } from 'react'
import { useCatalogo } from '../../core/hooks/useCatalogo'
import type {
  AccionDefinida, PreguntaDeTanda, TandaPedida, VeredictoDeRespuesta,
} from '../../core/models/catalogo.model'
import { capturarDictado } from '../../core/services/tablasApi'
import {
  accionesDelSpot, pedirTanda, responder, responderHablado,
} from '../../core/services/entrenadorApi'
import { BotonesDeAccion } from './BotonesDeAccion'
import { FiltroDeTanda } from './FiltroDeTanda'
import { MesaSimulada } from './MesaSimulada'
import { useCantarPregunta } from './useCantarPregunta'
import { Veredicto } from './Veredicto'

const PEDIDA_INICIAL: TandaPedida = {
  formato: null, situacion: null, minBB: null, maxBB: null, spot: null, tamano: 20,
}

/**
 * El bucle del entrenador: filtro → tanda → pregunta → veredicto → siguiente.
 *
 * A diferencia del resto de la app, esto NO anda sin base de datos: un
 * calendario de repetición que pierde respuestas no es un calendario. Por eso
 * el error se muestra en pantalla en lugar de tragarse, que es lo que hacen la
 * bitácora y el diario.
 */
export function PaginaDeEntrenador() {
  const { catalogo } = useCatalogo()

  const [pedida, setPedida] = useState<TandaPedida>(PEDIDA_INICIAL)
  const [tanda, setTanda] = useState<PreguntaDeTanda[] | null>(null)
  const [indice, setIndice] = useState(0)
  const [acciones, setAcciones] = useState<AccionDefinida[]>([])
  const [veredicto, setVeredicto] = useState<VeredictoDeRespuesta | null>(null)
  const [aciertos, setAciertos] = useState(0)
  const [cargando, setCargando] = useState(false)
  const [error, setError] = useState<string | null>(null)
  // Mientras la respuesta viaja, la pregunta sigue en pantalla y el veredicto
  // todavía no llegó: sin esto, dos clicks rápidos mandan dos respuestas para
  // la misma casilla y le mueven el calendario dos veces. El estado maneja la
  // pantalla (deshabilitar botones, mostrar "cargando"); la puerta de verdad
  // es `contestandoRef` (ver más abajo).
  const [contestando, setContestando] = useState(false)
  // El efecto que escucha la voz sólo se vuelve a armar cuando cambia la
  // pregunta —no cuando cambia `contestando`—, así que su callback de
  // `capturarDictado().then(...)` queda con un `contestarHablando` de la
  // misma vieja renderización, con `contestando` congelado en `false` para
  // siempre. Un click que ponga `contestando` en `true` un instante después
  // es invisible para ese closure viejo: el estado no alcanza para cerrar la
  // carrera entre un click y una respuesta hablada casi simultáneos. Un ref
  // sí, porque `.current` es el mismo objeto para cualquier closure, viejo o
  // nuevo, y se lee en vivo.
  const contestandoRef = useRef(false)
  // La voz se enciende a mano. Entrenando en silencio —de noche, o al lado de
  // alguien— cantar cada pregunta es peor que no tenerla.
  const [conVoz, setConVoz] = useState(false)

  const pregunta = tanda?.[indice] ?? null

  // Los botones son los del spot de la pregunta, no una lista fija: cada spot
  // usa las acciones que usa.
  useEffect(() => {
    if (!pregunta) return
    let cancelado = false
    accionesDelSpot(pregunta.situacion, pregunta.claveDeStack, pregunta.spot)
      .then((a) => { if (!cancelado) setAcciones(a) })
      .catch((e: unknown) => {
        if (cancelado) return
        setAcciones([])
        // Sin esto, un fallo acá deja la mano en pantalla sin botones y sin
        // ninguna explicación: parece que la app se colgó. El entrenador es
        // el único módulo que no puede fallar en silencio.
        setError(e instanceof Error ? e.message : 'No se pudieron traer las acciones del spot.')
      })
    return () => { cancelado = true }
  }, [pregunta])

  const arrancar = async () => {
    setCargando(true)
    setError(null)
    try {
      const preguntas = await pedirTanda(pedida)
      setTanda(preguntas)
      setIndice(0)
      setAciertos(0)
      setVeredicto(null)
      if (preguntas.length === 0) setError('No hay nada para entrenar con ese filtro.')
    } catch (e) {
      setError(e instanceof Error ? e.message : 'No se pudo armar la tanda.')
    } finally {
      setCargando(false)
    }
  }

  const elegir = async (accion: string) => {
    // El chequeo y el cierre de la puerta van juntos y sin ningún `await` en
    // el medio: si el otro camino (voz o teclado) corre entre el chequeo y el
    // cierre, la puerta no protege nada.
    if (!pregunta || veredicto || contestandoRef.current) return
    contestandoRef.current = true
    setContestando(true)
    setError(null)
    try {
      const v = await responder({
        situacion: pregunta.situacion,
        claveDeStack: pregunta.claveDeStack,
        spot: pregunta.spot,
        mano: pregunta.mano,
        accion,
      })
      setVeredicto(v)
      if (v.acerto) setAciertos((previo) => previo + 1)
    } catch (e) {
      setError(e instanceof Error ? e.message : 'No se pudo guardar la respuesta.')
    } finally {
      contestandoRef.current = false
      setContestando(false)
    }
  }

  /**
   * El mismo camino que el teclado, pero desde lo que se oyó — y con la misma
   * puerta. Si el texto no era una acción, `responderHablado` devuelve null y
   * la pregunta sigue abierta —conversar al lado del micrófono no cuenta como
   * fallo—, pero la puerta tiene que volver a abrirse igual o la pantalla
   * queda trabada esperando una respuesta que no va a llegar; por eso el
   * `finally`.
   */
  const contestarHablando = async (texto: string) => {
    if (!pregunta || veredicto || contestandoRef.current) return
    contestandoRef.current = true
    setContestando(true)
    setError(null)
    try {
      const v = await responderHablado(
        pregunta.situacion, pregunta.claveDeStack, pregunta.spot, pregunta.mano, texto)
      if (!v) return
      setVeredicto(v)
      if (v.acerto) setAciertos((previo) => previo + 1)
    } catch (e) {
      setError(e instanceof Error ? e.message : 'No se pudo guardar la respuesta.')
    } finally {
      contestandoRef.current = false
      setContestando(false)
    }
  }

  // Escucha una respuesta por pregunta. Se reinicia con cada `pregunta`, y se
  // corta apenas hay veredicto para no oír la siguiente antes de tiempo.
  useEffect(() => {
    if (!pregunta || veredicto || !conVoz) return
    let cancelado = false
    void capturarDictado().then((r) => {
      if (!cancelado && r.texto) void contestarHablando(r.texto)
    })
    return () => { cancelado = true }
    // oxlint-disable-next-line exhaustive-deps
  }, [pregunta, veredicto, conVoz])

  // Mientras hay veredicto no se canta: se está leyendo la explicación.
  useCantarPregunta(veredicto ? null : pregunta, conVoz)

  const seguir = () => {
    setVeredicto(null)
    setContestando(false)
    setIndice((previo) => previo + 1)
  }

  const terminada = tanda !== null && indice >= tanda.length

  return (
    <div className="entrenador">
      <header className="entrenamiento-cabecera">
        <div>
          <h1>Entrenador</h1>
          <p className="subtitulo">Te pregunta, y al fallar te explica</p>
        </div>
        <button
          type="button"
          className={conVoz ? 'boton-principal' : 'boton-tenue'}
          onClick={() => setConVoz((previo) => !previo)}
        >
          {conVoz ? 'Voz encendida' : 'Voz apagada'}
        </button>
        {tanda && !terminada && (
          <p className="entrenador-marcador">
            {indice + 1} / {tanda.length} · {aciertos} bien
          </p>
        )}
      </header>

      {error && <p className="sin-entender-error">{error}</p>}

      {catalogo && (
        <FiltroDeTanda
          situaciones={catalogo.situaciones}
          pedida={pedida}
          onCambiar={setPedida}
          onArrancar={() => void arrancar()}
          cargando={cargando || contestando}
        />
      )}

      {terminada && tanda.length > 0 && (
        <p className="entrenador-final">
          Tanda terminada: {aciertos} de {tanda.length}.
        </p>
      )}

      {pregunta && (
        <>
          <MesaSimulada pregunta={pregunta} />
          <BotonesDeAccion
            acciones={acciones}
            deshabilitado={veredicto !== null || contestando}
            onElegir={(clave) => void elegir(clave)}
          />
          {veredicto && (
            <Veredicto veredicto={veredicto} acciones={acciones} onSeguir={seguir} />
          )}
        </>
      )}
    </div>
  )
}
