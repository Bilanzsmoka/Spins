import { useEffect, useRef, useState } from 'react'
import { useCatalogo } from '../../core/hooks/useCatalogo'
import type {
  AccionDefinida, PreguntaDeTanda, TandaPedida, VeredictoDeRespuesta,
} from '../../core/models/catalogo.model'
import type { ResultadoDeCaptura } from '../../core/services/tablasApi'
import {
  accionesDelSpot, ErrorDeApi, pedirTanda, responder, responderHablado,
} from '../../core/services/entrenadorApi'
import { BotonesDeAccion } from './BotonesDeAccion'
import { FiltroDeTanda } from './FiltroDeTanda'
import { MesaSimulada } from './MesaSimulada'
import { useCantarPregunta } from './useCantarPregunta'
import { Veredicto } from './Veredicto'

interface Props {
  /**
   * Escucha una respuesta y devuelve lo que se oyó. Llega de afuera —del mismo
   * hook que tiene el motor continuo— porque el micrófono es uno solo: abrir
   * acá un motor propio dejaba al copiloto escuchando en paralelo, oyendo tu
   * respuesta, mandándola a /api/voz/dictado y diciendo "No te entendí"
   * encima del entrenador.
   */
  onCapturar: () => Promise<ResultadoDeCaptura>

  /** La tabla del hito activo, cuando se llega desde el panel del día. */
  situacionInicial?: string | null
}

const PEDIDA_INICIAL: TandaPedida = {
  formato: null, situacion: null, minBB: null, maxBB: null, spot: null, tamano: 20,
}

/**
 * Una captura que vuelve sin texto casi al instante no es silencio: es el
 * micrófono negándose (ocupado, o el permiso todavía sin resolver). Re-armar
 * sobre eso giraría a máxima velocidad creando motores.
 */
const MINIMO_DE_ESCUCHA_MS = 500

/** Los motivos de corte que son silencio y no falla: se vuelve a escuchar. */
const SILENCIOS = ['silencio', 'no-speech']

/** Qué casilla es, para no reinyectarla dos veces en la misma tanda. */
const claveDeCasilla = (p: PreguntaDeTanda) =>
  `${p.situacion}|${p.claveDeStack}|${p.spot}|${p.mano}`

/**
 * El bucle del entrenador: filtro → tanda → pregunta → veredicto → siguiente.
 *
 * A diferencia del resto de la app, esto NO anda sin base de datos: un
 * calendario de repetición que pierde respuestas no es un calendario. Por eso
 * el error se muestra en pantalla en lugar de tragarse, que es lo que hacen la
 * bitácora y el diario.
 */
export function PaginaDeEntrenador({ onCapturar, situacionInicial }: Props) {
  const { catalogo, error: errorDeCatalogo } = useCatalogo()

  // Si venís del panel del día, la tanda arranca ya filtrada en la tabla del
  // hito activo: el módulo se monta de nuevo al cambiar, así que alcanza con
  // el valor inicial.
  const [pedida, setPedida] = useState<TandaPedida>(
    { ...PEDIDA_INICIAL, situacion: situacionInicial ?? null })
  const [tanda, setTanda] = useState<PreguntaDeTanda[] | null>(null)
  const [indice, setIndice] = useState(0)
  const [acciones, setAcciones] = useState<AccionDefinida[]>([])
  const [veredicto, setVeredicto] = useState<VeredictoDeRespuesta | null>(null)
  // Lo que tardaste en la que acabás de contestar, para mostrarlo. Verlo es lo
  // que hace que empieces a contestar más rápido; guardarlo en silencio no
  // cambia nada hoy.
  const [tardo, setTardo] = useState(0)
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
  // `onCapturar()` queda con un `contestarHablando` de la misma vieja
  // renderización, con `contestando` congelado en `false` para siempre. Un
  // click que ponga `contestando` en `true` un instante después es invisible
  // para ese closure viejo: el estado no alcanza para cerrar la carrera entre
  // un click y una respuesta hablada casi simultáneos. Un ref sí, porque
  // `.current` es el mismo objeto para cualquier closure, viejo o nuevo, y se
  // lee en vivo.
  const contestandoRef = useRef(false)
  // Las casillas que ya volvieron a entrar en esta tanda. Sin este registro,
  // fallar veinte de veinte haría crecer la tanda sin fin y el tamaño elegido
  // dejaría de significar algo: se reentra una sola vez por tanda.
  const reingresadas = useRef(new Set<string>())
  /**
   * Cuándo apareció la pregunta que está abierta. De acá sale el tiempo de
   * respuesta, que es la mitad de lo que define un reflejo: acertar en once
   * segundos y acertar en uno no son lo mismo, y hasta ahora se guardaban
   * igual.
   *
   * Va en un ref y no en estado porque leerlo no tiene que redibujar nada, y
   * porque los dos caminos que contestan —el click y la voz— lo leen desde
   * closures de renderizaciones distintas.
   */
  const desdeQueAparecio = useRef<number>(0)
  // La voz se enciende a mano. Entrenando en silencio —de noche, o al lado de
  // alguien— cantar cada pregunta es peor que no tenerla.
  const [conVoz, setConVoz] = useState(false)

  const pregunta = tanda?.[indice] ?? null

  // El reloj arranca cuando la pregunta aparece en pantalla, no cuando se
  // pidió la tanda: lo que se mide es cuánto tardás vos, no la red.
  useEffect(() => { desdeQueAparecio.current = performance.now() }, [pregunta])

  /** Cuánto tardaste, redondeado. Cero si por algo no se pudo medir. */
  const tardanza = () =>
    desdeQueAparecio.current > 0 ? Math.round(performance.now() - desdeQueAparecio.current) : 0

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
      reingresadas.current = new Set()
      if (preguntas.length === 0) setError('No hay nada para entrenar con ese filtro.')
    } catch (e) {
      setError(e instanceof Error ? e.message : 'No se pudo armar la tanda.')
    } finally {
      setCargando(false)
    }
  }

  /**
   * La casilla fallada vuelve al final de la tanda actual, no a la próxima: el
   * calendario ya la vence hoy, y verla de nuevo mientras la explicación está
   * fresca es el momento en que más sirve. El marcador crece —"14 / 21"— y
   * está bien: fallaste, así que te queda más por hacer.
   *
   * Una sola vez por tanda. Sin tope, veinte fallos harían una tanda infinita
   * y el tamaño pedido dejaría de ser una promesa.
   */
  const reinyectar = (fallada: PreguntaDeTanda) => {
    const clave = claveDeCasilla(fallada)
    if (reingresadas.current.has(clave)) return
    reingresadas.current.add(clave)
    setTanda((previa) => (previa ? [...previa, fallada] : previa))
  }

  /**
   * El 404 que el controlador documenta: la tabla se corrigió entre que se
   * armó la tanda y se contestó, así que esa casilla ya no existe. No es un
   * error del usuario y no tiene arreglo desde acá — quedarse en la pregunta
   * la dejaba trabada, con cualquier botón repitiendo el mismo 404 y sin más
   * salida que re-armar la tanda.
   */
  const saltearCasillaInexistente = () => {
    setError('Esa casilla ya no existe en el catálogo —la tabla cambió—: paso a la siguiente.')
    setVeredicto(null)
    setIndice((previo) => previo + 1)
  }

  /** Qué hacer con lo que tiró el servidor: saltear la pregunta, o mostrarlo. */
  const manejarFallo = (e: unknown, porDefecto: string) => {
    if (e instanceof ErrorDeApi && e.estado === 404) saltearCasillaInexistente()
    else setError(e instanceof Error ? e.message : porDefecto)
  }

  const elegir = async (accion: string) => {
    // El chequeo y el cierre de la puerta van juntos y sin ningún `await` en
    // el medio: si el otro camino (voz o teclado) corre entre el chequeo y el
    // cierre, la puerta no protege nada.
    if (!pregunta || veredicto || contestandoRef.current) return
    contestandoRef.current = true
    setContestando(true)
    setError(null)
    const ms = tardanza()
    setTardo(ms)
    try {
      const v = await responder({
        situacion: pregunta.situacion,
        claveDeStack: pregunta.claveDeStack,
        spot: pregunta.spot,
        mano: pregunta.mano,
        accion,
        milisegundos: ms,
      })
      setVeredicto(v)
      if (v.acerto) setAciertos((previo) => previo + 1)
      else reinyectar(pregunta)
    } catch (e) {
      manejarFallo(e, 'No se pudo guardar la respuesta.')
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
   *
   * Devuelve si la pregunta quedó cerrada: quien escucha necesita saberlo para
   * volver a oír cuando lo que se dijo no era una acción.
   */
  const contestarHablando = async (texto: string): Promise<boolean> => {
    if (!pregunta || veredicto || contestandoRef.current) return false
    contestandoRef.current = true
    setContestando(true)
    setError(null)
    const ms = tardanza()
    setTardo(ms)
    try {
      const v = await responderHablado(
        pregunta.situacion, pregunta.claveDeStack, pregunta.spot, pregunta.mano, texto, ms)
      if (!v) return false
      setVeredicto(v)
      if (v.acerto) setAciertos((previo) => previo + 1)
      else reinyectar(pregunta)
      return true
    } catch (e) {
      manejarFallo(e, 'No se pudo guardar la respuesta.')
      return true
    } finally {
      contestandoRef.current = false
      setContestando(false)
    }
  }

  // Escucha mientras la pregunta esté abierta, y se corta apenas hay veredicto
  // para no oír la siguiente antes de tiempo.
  //
  // El motor es el del hook de voz —el dueño del micrófono— y no uno propio:
  // así la escucha continua queda pausada mientras se graba, en vez de oír la
  // respuesta, mandarla como consulta y contestar "No te entendí" encima.
  //
  // Se vuelve a escuchar en bucle porque una captura resuelve una sola vez:
  // Chrome la corta a los pocos segundos de silencio y, sin re-armar, tardar
  // en contestar dejaba la voz muerta hasta la pregunta siguiente. Sólo se
  // re-arma tras un silencio: un error (permiso, micrófono ocupado) se muestra
  // y se para, o el bucle giraría contra la misma falla para siempre.
  useEffect(() => {
    if (!pregunta || veredicto || !conVoz) return
    let cancelado = false

    const escuchar = async () => {
      while (!cancelado) {
        const desde = Date.now()
        const { texto, motivo } = await onCapturar()
        // La bandera se relee después de cada espera: cambiar de pregunta o
        // apagar la voz tiene que cortar el bucle, no una vuelta más.
        if (cancelado) return

        if (texto) {
          // Si no era una acción la pregunta sigue abierta: se vuelve a oír.
          if (await contestarHablando(texto)) return
          continue
        }

        if (!SILENCIOS.includes(motivo ?? '')) {
          setError(`No pude escuchar (${motivo}).`)
          return
        }
        if (Date.now() - desde < MINIMO_DE_ESCUCHA_MS) {
          setError('El micrófono no está disponible para el entrenador.')
          return
        }
      }
    }

    void escuchar()
    return () => { cancelado = true }
    // oxlint-disable-next-line exhaustive-deps
  }, [pregunta, veredicto, conVoz, onCapturar])

  // Mientras hay veredicto no se canta: se está leyendo la explicación.
  useCantarPregunta(veredicto ? null : pregunta, conVoz)

  const seguir = () => {
    setVeredicto(null)
    setContestando(false)
    setIndice((previo) => previo + 1)
  }

  const terminada = tanda !== null && indice >= tanda.length

  // De acá salen los filtros y la tanda: sin catálogo no hay nada que ofrecer.
  // Tragarse el error dejaba el encabezado solo —sin mensaje y sin botón—,
  // como si la app se hubiera colgado.
  if (errorDeCatalogo)
    return <p className="error">No pude cargar el catálogo: {errorDeCatalogo}</p>

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
            <Veredicto
              veredicto={veredicto}
              acciones={acciones}
              milisegundos={tardo}
              onSeguir={seguir}
            />
          )}
        </>
      )}
    </div>
  )
}
