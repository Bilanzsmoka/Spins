import { useEffect, useState } from 'react'
import type { ConsultaRegistrada, EventoDeVoz } from '../models/catalogo.model'

/** Cuántas consultas se conservan escritas en pantalla. */
const MAXIMO_HISTORIAL = 30

/**
 * Cuántas frases sin entender se ofrecen para enseñar. Cortas a propósito:
 * la lista es para las de la tanda que acabás de dictar, no un archivo.
 */
const MAXIMO_SIN_ENTENDER = 8

/** Una frase que llegó y el intérprete rechazó, esperando que le digas qué era. */
export interface FraseSinEntender {
  texto: string
  hora: string
}

/**
 * Se suscribe al canal SSE del copiloto. EventSource reconecta solo,
 * asi que no hace falta watchdog del lado del navegador.
 *
 * Ademas del ultimo evento guarda un historial acotado: la respuesta se
 * habla, pero hablada se pierde. Escrita queda, y sirve para repasar la
 * tanda al terminar de jugar.
 *
 * Y aparte, las frases que NO se entendieron. Van en su propia lista porque
 * su destino es otro: el historial se lee, estas se corrigen. Reconocer una
 * palabra mal es lo normal las primeras veces —Chrome no conoce "be be" ni
 * "min raise"—, y el arreglo es enseñarle cómo lo decís vos. Juntarlas en vez
 * de preguntar en el momento es lo que permite dictar sin manos: seguís
 * estudiando y se las enseñás todas cuando volvés al teclado.
 */
export function useEventosDeVoz() {
  const [ultimo, setUltimo] = useState<EventoDeVoz | null>(null)
  const [historial, setHistorial] = useState<ConsultaRegistrada[]>([])
  const [sinEntender, setSinEntender] = useState<FraseSinEntender[]>([])
  const [conectado, setConectado] = useState(false)

  useEffect(() => {
    const fuente = new EventSource('/api/voz/eventos')
    fuente.onopen = () => setConectado(true)
    fuente.onerror = () => setConectado(false)
    fuente.onmessage = (mensaje) => {
      const evento = JSON.parse(mensaje.data) as EventoDeVoz
      setUltimo(evento)
      const hora = new Date().toLocaleTimeString('es', {
        hour: '2-digit', minute: '2-digit', second: '2-digit',
      })
      // Lo que no se entendió NO entra al historial: ese es el registro de lo
      // que estudiaste —manos y spots—, y llenarlo de "no entendí" lo
      // convierte en un cajón de basura donde ya no se puede repasar la
      // tanda. La frase fallida se ve igual, en dos lados mejores: el cartel
      // de arriba, que muestra el último evento, y su propio panel, que
      // además deja enseñarla.
      if (evento.tipo !== 'Ignorado')
        setHistorial((previo) => [{ ...evento, hora }, ...previo].slice(0, MAXIMO_HISTORIAL))

      if (evento.tipo !== 'Ignorado' || !evento.textoCrudo.trim()) return
      // Sin dedup, decir tres veces la misma palabra mal reconocida deja tres
      // filas que enseñan exactamente lo mismo. Se reordena al frente para que
      // lo último que te falló quede arriba.
      setSinEntender((previo) => [
        { texto: evento.textoCrudo, hora },
        ...previo.filter((f) => f.texto !== evento.textoCrudo),
      ].slice(0, MAXIMO_SIN_ENTENDER))
    }
    return () => fuente.close()
  }, [])

  const limpiarHistorial = () => setHistorial([])

  /** La sacás de la lista: se la enseñaste, o no valía la pena. */
  const olvidarFrase = (texto: string) =>
    setSinEntender((previo) => previo.filter((f) => f.texto !== texto))

  return { ultimo, historial, sinEntender, conectado, limpiarHistorial, olvidarFrase }
}
