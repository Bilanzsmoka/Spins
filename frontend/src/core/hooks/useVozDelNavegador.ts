import { useCallback, useEffect, useRef, useState } from 'react'
import type { EventoDeVoz } from '../models/catalogo.model'
import { capturarDictado, enviarDictado } from '../services/tablasApi'

/** La API vive con prefijo en Chrome y sin prefijo en el estándar. */
type ConstructorDeReconocimiento = new () => SpeechRecognition
const Reconocimiento: ConstructorDeReconocimiento | undefined =
  (window as unknown as { SpeechRecognition?: ConstructorDeReconocimiento }).SpeechRecognition
  ?? (window as unknown as { webkitSpeechRecognition?: ConstructorDeReconocimiento }).webkitSpeechRecognition

/**
 * Cuanto se espera, despues de un pedazo de frase, por si viene otro.
 *
 * Es el tiempo que separa "todavia esta hablando" de "ya termino". Corto de
 * mas parte las frases —que es el problema que esto viene a resolver—; largo
 * de mas hace esperar la respuesta de cada consulta. Como la app deja de
 * escuchar mientras habla, entre una consulta y la siguiente siempre hay un
 * hueco mas grande que este.
 */
const ESPERA_ENTRE_PEDAZOS = 900

/**
 * El copiloto del lado del navegador: oye con la Web Speech API y habla con
 * speechSynthesis.
 *
 * Los dos van juntos por una razón concreta: mientras la app habla hay que
 * dejar de escuchar, o el micrófono toma la respuesta y dispara una consulta
 * con la propia voz de la app. Teniendo las dos puntas acá, silenciar es
 * apagar el reconocimiento durante la frase.
 *
 * Recibe el evento entero y no su texto a propósito: ver el efecto que habla.
 */
export function useVozDelNavegador(evento: EventoDeVoz | null) {
  const [activo, setActivo] = useState(false)
  // Si el motor esta vivo AHORA, que no es lo mismo que si vos lo prendiste.
  // Chrome lo corta solo cada tanto y un fatal lo mata del todo; sin esto la
  // pantalla decia "Escuchando" con el reconocedor muerto, y hablabas creyendo
  // que te oia.
  const [escuchando, setEscuchando] = useState(false)
  /**
   * Lo último que hizo el motor, en crudo. No es lo mismo que `falla`: acá
   * entran también los arranques y los `no-speech`, que no son errores.
   *
   * Existe porque cuando la voz no anda, lo que hay en pantalla es un cartel
   * que no distingue "Chrome no me da el micrófono" de "arrancó y nadie
   * habló", y sin esa diferencia no hay nada que hacer salvo adivinar.
   */
  const [ultimoEvento, setUltimoEvento] = useState<string | null>(null)
  const [falla, setFalla] = useState<string | null>(null)
  const [fallaAlHablar, setFallaAlHablar] = useState<string | null>(null)
  const motor = useRef<SpeechRecognition | null>(null)
  // Mientras esto está en true el motor continuo está parado a propósito: o la
  // app está hablando, o alguien más tomó el micrófono para una captura.
  const silenciado = useRef(false)
  // La frase que suena ahora. Hay que poder desengancharle los handlers antes
  // de cancelarla; ver el efecto que habla.
  const fraseEnCurso = useRef<SpeechSynthesisUtterance | null>(null)
  const ultimoHablado = useRef<EventoDeVoz | null>(null)
  // Los pedazos de una misma frase, esperando a que deje de llegar mas. Ver
  // ESPERA_ENTRE_PEDAZOS.
  const pedazos = useRef<string[]>([])
  const confianzaMinima = useRef(1)
  const plazoDeEnvio = useRef<number | null>(null)

  const disponible = Reconocimiento !== undefined

  useEffect(() => {
    if (!disponible || !activo) return

    const r = new Reconocimiento!()
    r.lang = 'es-ES'
    r.continuous = true
    // Los parciales son una frase a medio formar: resolverlos daría
    // respuestas contra manos que todavía no se terminaron de decir.
    r.interimResults = false

    // Chrome corta la escucha continua sola cada tanto y hay que reengancharla,
    // pero onend tambien dispara despues de un error fatal: sin distinguirlos,
    // un permiso denegado entra en start -> error -> end -> start sin freno.
    const fatales = ['not-allowed', 'service-not-allowed', 'audio-capture']
    let ultimoError: string | null = null

    r.onresult = (evento) => {
      // Un resultado bueno demuestra que el permiso sigue en pie: un fatal
      // viejo no debe seguir bloqueando el reenganche.
      ultimoError = null
      setUltimoEvento('oí algo')
      if (silenciado.current) return
      const ultimo = evento.results[evento.results.length - 1]
      if (!ultimo.isFinal) return

      // Chrome cierra un pedazo cuando esta seguro de esas palabras, no
      // cuando vos terminaste la frase: "as rey suited" llega como "as rey" y
      // despues "suited". Mandando cada pedazo, la mano se resolvia sin el
      // palo —y sin palo la regla asume offsuit—, y el "suited" que venia
      // atras llegaba solo y se descartaba por no ser una orden. Salia la
      // accion de otra casilla y todo se veia normal.
      pedazos.current.push(ultimo[0].transcript.trim())
      confianzaMinima.current = Math.min(
        confianzaMinima.current, ultimo[0].confidence || 0.9)

      if (plazoDeEnvio.current !== null) clearTimeout(plazoDeEnvio.current)
      plazoDeEnvio.current = window.setTimeout(() => {
        plazoDeEnvio.current = null
        const frase = pedazos.current.join(' ').trim()
        const confianza = confianzaMinima.current
        pedazos.current = []
        confianzaMinima.current = 1
        // Callado quiere decir que el microfono es de otro —la app hablando, o
        // una captura de vocabulario—: lo que se junto antes ya no es una
        // orden que corresponda contestar.
        if (silenciado.current || frase.length === 0) return
        void enviarDictado(frase, confianza)
      }, ESPERA_ENTRE_PEDAZOS)
    }
    r.onerror = (evento) => {
      ultimoError = evento.error
      setUltimoEvento(`error: ${evento.error}`)
      // "no-speech" es silencio, no una falla: Chrome lo emite todo el tiempo.
      if (evento.error !== 'no-speech') setFalla(evento.error)
    }
    r.onstart = () => { setEscuchando(true); setUltimoEvento('arrancó') }
    r.onend = () => {
      // Un fatal no se recupera solo: ni el permiso vuelve ni el microfono
      // aparece porque reintentemos. Y `ultimoError` solo se limpia en
      // onresult, que ya no puede ocurrir con el motor parado — asi que sin
      // esto la voz quedaba muerta para siempre mientras el interruptor
      // seguia en "encendido". Se apaga de verdad, para que se pueda volver
      // a prender cuando el problema se resuelva.
      if (ultimoError !== null && fatales.includes(ultimoError))
      {
        setEscuchando(false);
        setActivo(false);
        setUltimoEvento(`se apagó por: ${ultimoError}`);
        return;
      }

      // Lo normal: Chrome corta la escucha continua sola y se reengancha.
      if (activo && !silenciado.current)
      {
        try { r.start() } catch { setEscuchando(false) }
        return;
      }

      setEscuchando(false);
    }

    motor.current = r
    // oxlint-disable-next-line set-state-in-effect
    setUltimoEvento('pidiendo el micrófono…')
    try { r.start() } catch (e) { setFalla(String(e)); setEscuchando(false) }

    // Se anulan los tres handlers, no solo onend: stop() no es instantáneo y
    // un resultado que ya estaba en el buffer llegaría después de apagar la
    // voz, contestando sola justo cuando se le dijo que se callara.
    return () => {
      // Un envio pendiente despues de apagar la voz es una consulta que sale
      // sola justo cuando se le dijo que se callara: la misma razon por la que
      // se anulan los handlers.
      if (plazoDeEnvio.current !== null) clearTimeout(plazoDeEnvio.current)
      plazoDeEnvio.current = null
      pedazos.current = []
      confianzaMinima.current = 1

      motor.current = null
      r.onresult = null
      r.onerror = null
      r.onend = null
      r.onstart = null
      setEscuchando(false)
      r.stop()
    }
  }, [disponible, activo])

  // Hablar la respuesta, con el micrófono apagado mientras dura.
  //
  // La dependencia es el evento entero y no `evento.respuesta`: React compara
  // con Object.is, y dos manos distintas dan la misma respuesta todo el tiempo
  // ("FOLD." a secas cuando el palo se dictó). Con el texto como dependencia
  // la segunda no se hablaba — la celda se resaltaba igual, así que en
  // pantalla parecía andar y lo que se rompía era justo estudiar sin mirar.
  useEffect(() => {
    // El evento se marca como hablado aunque la voz esté apagada: si no, al
    // encenderla se soltaría de golpe la respuesta de una consulta vieja.
    const yaHablado = ultimoHablado.current === evento
    ultimoHablado.current = evento
    if (!evento?.respuesta || !activo || yaHablado) return

    silenciado.current = true
    motor.current?.stop()

    // speak() encola en vez de reemplazar, así que hay que cancelar. Pero
    // antes se le sacan los handlers a la frase anterior: el estándar dice
    // que cancel() emite `error`, y Chrome viene emitiendo `end` desde hace
    // varias versiones. Ese `end` viejo devolvería el micrófono a escuchar
    // mientras la frase nueva todavía suena, y la app se oiría a sí misma.
    const anterior = fraseEnCurso.current
    if (anterior) { anterior.onend = null; anterior.onerror = null }
    window.speechSynthesis.cancel()

    const frase = new SpeechSynthesisUtterance(evento.respuesta)
    frase.lang = 'es-ES'

    const terminar = () => {
      // Si ya la reemplazó otra frase, el micrófono le pertenece a esa.
      if (fraseEnCurso.current !== frase) return
      fraseEnCurso.current = null
      silenciado.current = false
      try { motor.current?.start() } catch { /* ya corriendo */ }
    }

    frase.onend = () => { setFallaAlHablar(null); terminar() }
    // Sin onerror, una síntesis que falla (synthesis-failed, audio-busy, la
    // pestaña suspendida de fondo) dejaba `silenciado` en true para siempre:
    // todo dictado se descartaba y el micrófono no se reenganchaba nunca más.
    // La voz moría en silencio hasta recargar la página.
    frase.onerror = (e) => { setFallaAlHablar(e.error); terminar() }

    fraseEnCurso.current = frase
    window.speechSynthesis.speak(frase)
  }, [evento, activo])

  /**
   * Toma el micrófono para una sola frase y devuelve lo que se oyó, sin
   * interpretar. Vive acá, y no suelto en la página que lo usa, porque el
   * micrófono es uno solo: con el motor continuo escuchando en paralelo, o
   * éste oye la palabra que se está enseñando y la manda como dictado
   * —cambiando la tabla en estudio— o el start() de la captura falla. Siendo
   * el dueño del motor quien la expone, es imposible olvidarse de pausarlo.
   *
   * Se usa abort() y no stop(): la captura necesita el micrófono ya, y de un
   * resultado a medio formar del motor continuo no queremos nada.
   */
  const capturar = useCallback(async () => {
    silenciado.current = true

    // Abortar no libera el microfono en el mismo tick: Chrome lo suelta al
    // emitir `end`. Arrancar la captura antes de eso hacia que su start()
    // fallara contra un microfono todavia ocupado, y la pantalla mostraba un
    // "no capte nada" que en realidad era "el otro motor no habia soltado".
    // El plazo de respaldo existe porque si el motor ya estaba parado, ese
    // `end` no llega nunca.
    const activo = motor.current
    if (activo) {
      await new Promise<void>((seguir) => {
        const plazo = setTimeout(seguir, 300)
        activo.onend = () => { clearTimeout(plazo); seguir() }
        activo.abort()
      })
    }

    try {
      return await capturarDictado()
    } finally {
      silenciado.current = false
      try { motor.current?.start() } catch { /* ya corriendo */ }
    }
  }, [])

  const alternar = useCallback(() => {
    setFalla(null)
    setFallaAlHablar(null)
    setActivo((previo) => !previo)
  }, [])

  return {
    disponible, activo, escuchando, ultimoEvento, falla, fallaAlHablar, alternar, capturar,
  }
}
