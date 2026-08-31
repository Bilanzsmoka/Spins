import { useEffect, useRef, useState } from 'react'
import { useCatalogo } from '../../core/hooks/useCatalogo'
import type {
  AccionDefinida, PreguntaDeTanda, TandaPedida, TerminoDelGlosario, VeredictoDeRespuesta,
} from '../../core/models/catalogo.model'
import type { ResultadoDeCaptura } from '../../core/services/tablasApi'
import {
  accionesDelSpot, ErrorDeApi, pedirTanda, responder, responderHablado,
} from '../../core/services/entrenadorApi'
import { obtenerGlosario } from '../../core/services/tablasApi'
import { BotonesDeAccion } from './BotonesDeAccion'
import { FiltroDeTanda } from './FiltroDeTanda'
import { HistorialDeTanda, type ManoContestada } from './HistorialDeTanda'
import { MapaDeErrores } from './MapaDeErrores'
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

/**
 * Cuánto se muestra el "Bien" antes de pasar sola a la mano siguiente.
 *
 * Al acertar no hay nada que leer: el botón de seguir era un click de más cada
 * vez, y en una tanda de diez son diez. Al fallar NO se avanza solo — ahí sí
 * hay una explicación que leer, y apurarla sería perder justo el momento en
 * que más entra.
 */
const PAUSA_AL_ACERTAR_MS = 650

/** Cuántas manos se guardan a la vista. Más no se miran. */
const MAXIMO_HISTORIAL = 40

/** Sin límite: la tanda se renueva sola cuando se termina. */
const SIN_LIMITE = 0

/** Lo que se le pide al servidor cuando no hay límite: su techo. */
const TANDA_LARGA = 100

const PEDIDA_INICIAL: TandaPedida = {
  formato: null, situacion: null, minBB: null, maxBB: null, spot: null, tamano: 10,
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
  // Lo contestado en esta tanda. Existe porque al acertar la mesa pasa sola:
  // sin esto, la mano que acabás de resolver desaparece sin dejar dónde mirarla.
  const [historial, setHistorial] = useState<ManoContestada[]>([])
  // Cuántas llevás contestadas en total. En modo sin límite la tanda se
  // renueva, así que el índice vuelve a cero y solo este número sigue subiendo.
  const [respondidas, setRespondidas] = useState(0)
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
  // El reloj es información, no castigo: cuenta hacia arriba y no reprueba
  // nada. Verlo es lo que empieza a cambiar cómo contestás; que además decida
  // si sabés una casilla es otra etapa.
  const [conReloj, setConReloj] = useState(true)
  const [transcurrido, setTranscurrido] = useState(0)
  // Los perfiles del glosario, para pintar cada rival de su color. Se piden una
  // vez: si el glosario no está, la mesa se dibuja igual, sin colores.
  const [perfiles, setPerfiles] = useState<TerminoDelGlosario[]>([])

  useEffect(() => {
    let cancelado = false
    obtenerGlosario()
      .then((g) => {
        if (!cancelado) setPerfiles(g.find((x) => x.clave === 'jugadores')?.terminos ?? [])
      })
      .catch(() => { if (!cancelado) setPerfiles([]) })
    return () => { cancelado = true }
  }, [])

  const pregunta = tanda?.[indice] ?? null

  // El reloj arranca cuando la pregunta aparece en pantalla, no cuando se
  // pidió la tanda: lo que se mide es cuánto tardás vos, no la red.
  useEffect(() => { desdeQueAparecio.current = performance.now() }, [pregunta])

  // Corre mientras la pregunta está abierta y se frena con el veredicto: seguir
  // contando mientras leés la explicación no mediría nada.
  useEffect(() => {
    // Se pone en cero acá y no en el tic: entre que aparece la pregunta y el
    // primer tic pasan 100 ms, y en esos 100 ms se vería el tiempo de la mano
    // anterior.
    // oxlint-disable-next-line set-state-in-effect
    setTranscurrido(0)
    if (!pregunta || veredicto || !conReloj) return
    const tic = setInterval(
      () => setTranscurrido(performance.now() - desdeQueAparecio.current), 100)
    return () => clearInterval(tic)
  }, [pregunta, veredicto, conReloj])

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

  const sinLimite = pedida.tamano === SIN_LIMITE

  /**
   * Trae una tanda. `sigue` es el modo sin límite renovándose: ahí no se
   * reinician ni los aciertos ni el historial, porque para el que entrena es
   * la misma sesión — lo único que pasó es que se acabó el lote.
   */
  const arrancar = async (sigue = false) => {
    setCargando(true)
    setError(null)
    try {
      const preguntas = await pedirTanda(
        sinLimite ? { ...pedida, tamano: TANDA_LARGA } : pedida)
      setTanda(preguntas)
      setIndice(0)
      setVeredicto(null)
      reingresadas.current = new Set()
      if (!sigue) {
        setAciertos(0)
        setRespondidas(0)
        setHistorial([])
      }
      if (preguntas.length === 0)
        setError(sigue
          ? 'No queda nada más para entrenar con ese filtro.'
          : 'No hay nada para entrenar con ese filtro.')
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

  /**
   * Deja la mano en el historial. La respuesta hablada no sabe qué clave se
   * dijo —la interpreta el servidor—, así que ahí se anota la correcta: en un
   * acierto es la misma, y en un fallo la corrección igual queda visible.
   */
  const anotar = (
    p: PreguntaDeTanda, elegida: string, v: VeredictoDeRespuesta, ms: number,
  ) => {
    setRespondidas((previo) => previo + 1)
    setHistorial((previo) => [{
      mano: p.mano,
      elegida,
      correcta: v.accionCorrecta,
      acerto: v.acerto,
      cerca: v.cerca,
      milisegundos: ms,
      etiquetaDeSpot: p.etiquetaDeSpot,
    }, ...previo].slice(0, MAXIMO_HISTORIAL))
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
      anotar(pregunta, accion, v, ms)
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
      anotar(pregunta, v.accionCorrecta, v, ms)
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

  /*
   * Al acertar no hay nada que leer, así que pasa sola. Los setters van
   * directo y no por `seguir` porque `seguir` se recrea en cada render: con él
   * en las dependencias, el plazo se reiniciaría cada vez y no llegaría a
   * dispararse nunca.
   */
  useEffect(() => {
    if (!veredicto?.acerto) return
    const plazo = setTimeout(() => {
      setVeredicto(null)
      setContestando(false)
      setIndice((previo) => previo + 1)
    }, PAUSA_AL_ACERTAR_MS)
    return () => clearTimeout(plazo)
  }, [veredicto])

  // Con la explicación en pantalla, Enter o espacio siguen. Es lo que hace
  // cualquier entrenador y evita ir al mouse por una tecla que ya tenés debajo
  // de los dedos. Los botones de acción no escuchan mientras hay veredicto,
  // así que no hay dos cosas peleando por la misma tecla.
  useEffect(() => {
    if (!veredicto || veredicto.acerto) return
    const alTeclear = (evento: KeyboardEvent) => {
      const donde = evento.target as HTMLElement | null
      const editando = donde !== null
        && ['input', 'textarea', 'select'].includes(donde.tagName.toLowerCase())
      if (editando || evento.ctrlKey || evento.altKey || evento.metaKey) return
      if (evento.key !== 'Enter' && evento.key !== ' ') return
      evento.preventDefault()
      setVeredicto(null)
      setContestando(false)
      setIndice((previo) => previo + 1)
    }
    window.addEventListener('keydown', alTeclear)
    return () => window.removeEventListener('keydown', alTeclear)
  }, [veredicto])

  // Sin límite: cuando el lote se acaba, entra el siguiente sin preguntar. Es
  // sincronizar con algo de afuera —el servidor tiene más material— y no un
  // estado que se pueda derivar durante el render.
  useEffect(() => {
    if (!sinLimite || !terminada || cargando || tanda?.length === 0) return
    // oxlint-disable-next-line set-state-in-effect, exhaustive-deps
    void arrancar(true)
    // oxlint-disable-next-line exhaustive-deps
  }, [sinLimite, terminada])

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
        <button
          type="button"
          className={conReloj ? 'boton-principal' : 'boton-tenue'}
          onClick={() => setConReloj((previo) => !previo)}
        >
          {conReloj ? 'Reloj encendido' : 'Reloj apagado'}
        </button>
        {tanda && !terminada && (
          <p className="entrenador-marcador">
            {sinLimite
              ? `${respondidas + 1} · ${aciertos} bien`
              : `${indice + 1} / ${tanda.length} · ${aciertos} bien`}
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

      {/*
        Antes de arrancar y al terminar: los dos momentos en que se puede mirar
        sin interrumpir nada. Las acciones salen del catálogo y no del spot
        —que todavía no existe— para que cada error salga con su color.
      */}
      {(!tanda || terminada) && catalogo && (
        <MapaDeErrores
          situaciones={catalogo.situaciones}
          acciones={catalogo.acciones}
          refrescar={terminada ? 1 : 0}
        />
      )}

      {terminada && tanda.length > 0 && (
        <p className="entrenador-final">
          Tanda terminada: {aciertos} de {tanda.length}.
        </p>
      )}

      {pregunta && (
        <>
          <MesaSimulada
            pregunta={pregunta}
            situacion={catalogo?.situaciones.find((s) => s.clave === pregunta.situacion) ?? null}
            perfiles={perfiles}
            milisegundos={conReloj ? transcurrido : 0}
          />
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

      {catalogo && <HistorialDeTanda manos={historial} acciones={catalogo.acciones} />}
    </div>
  )
}
