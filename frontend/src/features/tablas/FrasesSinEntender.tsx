import { useState } from 'react'
import type { FraseSinEntender } from '../../core/hooks/useEventosDeVoz'
import type {
  CategoriaDeVocabulario, SituacionResumen,
} from '../../core/models/catalogo.model'
import { agregarDicho } from '../../core/services/tablasApi'

interface Props {
  frases: FraseSinEntender[]
  situaciones: SituacionResumen[]
  situacion: string
  stack: string
  spot: string
  onOlvidar: (texto: string) => void
}

/** Una cosa que la frase pudo haber querido decir, tomada de la pantalla. */
interface Destino {
  titulo: string
  categoria: CategoriaDeVocabulario
  clave: string
  etiqueta: string
}

/**
 * Las frases que el intérprete rechazó, con un botón para enseñárselas.
 *
 * El reconocimiento del navegador no conoce la jerga: "be be contra min
 * raise" le sale "vivir versus race", y la frase entera se descarta. La
 * salida no es que yo adivine más variantes —son infinitas y dependen de
 * cómo hablás vos—, sino que la app aprenda de tu voz.
 *
 * Lo que lo hace de un solo click: cuando te entiende mal igual corregís el
 * selector a mano para seguir estudiando, y en ese momento la pantalla YA
 * sabe qué quisiste decir. Los botones ofrecen exactamente lo que tenés
 * abierto. El texto queda editable para el caso en que sobre algo ("12
 * vivir" cuando lo que hay que enseñar es "vivir").
 *
 * No se ofrece el stack: un stack es un número más una palabra ("doce be
 * be"), no una frase que mapee a una clave. Si lo que falla es esa palabra
 * se agrega desde Ajustes › Voz, que graba y compara.
 */
export function FrasesSinEntender({
  frases, situaciones, situacion, stack, spot, onOlvidar,
}: Props) {
  const [textos, setTextos] = useState<Record<string, string>>({})
  const [guardando, setGuardando] = useState<string | null>(null)
  const [error, setError] = useState<string | null>(null)

  const situacionActiva = situaciones.find((s) => s.clave === situacion)
  const spotActivo = situacionActiva?.stacks
    .find((t) => t.clave === stack)?.spots
    .find((p) => p.clave === spot)

  const destinos: Destino[] = []
  if (situacionActiva) {
    destinos.push({
      titulo: 'Formato',
      categoria: 'Formatos',
      clave: situacionActiva.formato,
      etiqueta: situacionActiva.formato,
    })
    destinos.push({
      titulo: 'Situación',
      categoria: 'Situaciones',
      clave: situacionActiva.clave,
      etiqueta: situacionActiva.etiqueta,
    })
  }
  if (spotActivo) {
    destinos.push({
      titulo: 'Spot',
      categoria: 'Spots',
      clave: spotActivo.clave,
      etiqueta: spotActivo.etiqueta,
    })
  }

  if (frases.length === 0) return null

  const ensenar = async (frase: FraseSinEntender, destino: Destino) => {
    const dicho = (textos[frase.texto] ?? frase.texto).trim()
    if (!dicho) return
    setGuardando(frase.texto)
    setError(null)
    try {
      await agregarDicho(destino.categoria, destino.clave, dicho)
      // El vocabulario del servidor es vivo: la próxima vez que lo digas ya
      // entra, sin reiniciar nada.
      onOlvidar(frase.texto)
    } catch (e) {
      setError(e instanceof Error ? e.message : 'No se pudo guardar.')
    } finally {
      setGuardando(null)
    }
  }

  return (
    <section className="sin-entender">
      <header className="sin-entender-titulo">
        <h3>Esto no lo entendí</h3>
        <p>
          Decile qué era. Los botones traen lo que tenés abierto en pantalla.
        </p>
      </header>

      {error && <p className="sin-entender-error">{error}</p>}

      <ul className="sin-entender-lista">
        {frases.map((frase) => (
          <li key={frase.texto} className="sin-entender-fila">
            <span className="sin-entender-hora">{frase.hora}</span>

            <input
              className="sin-entender-texto"
              value={textos[frase.texto] ?? frase.texto}
              onChange={(e) =>
                setTextos((previo) => ({ ...previo, [frase.texto]: e.target.value }))}
              aria-label={`Lo que se oyó: ${frase.texto}`}
            />

            <div className="sin-entender-destinos">
              {destinos.map((destino) => (
                <button
                  key={destino.titulo}
                  type="button"
                  className="sin-entender-destino"
                  disabled={guardando !== null}
                  onClick={() => void ensenar(frase, destino)}
                  title={`Guardar como forma de ${destino.etiqueta}`}
                >
                  <span className="sin-entender-destino-titulo">{destino.titulo}</span>
                  <span className="sin-entender-destino-etiqueta">{destino.etiqueta}</span>
                </button>
              ))}
            </div>

            <button
              type="button"
              className="sin-entender-descartar"
              onClick={() => onOlvidar(frase.texto)}
              title="No era una orden, descartala"
              aria-label={`Descartar «${frase.texto}»`}
            >
              ×
            </button>
          </li>
        ))}
      </ul>
    </section>
  )
}
