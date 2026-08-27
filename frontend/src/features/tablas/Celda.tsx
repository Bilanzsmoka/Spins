import type { AccionDefinida, ParteDeMix } from '../../core/models/catalogo.model'

interface Props {
  mano: string
  accion: AccionDefinida | undefined
  mix: ParteDeMix[] | null
  acciones: AccionDefinida[]
  resaltada: boolean
  onTocar?: (mano: string) => void
}

/**
 * Una celda de la matriz. El color nunca es la única señal: la etiqueta de la
 * mano va siempre visible encima.
 *
 * Una mano mixta se pinta partida en bandas verticales, una al lado de la otra,
 * con el ancho de cada una igual a su frecuencia: el mix ES un porcentaje, y
 * así un 70/30 se ve distinto de un 50/50 sin leer ningún número.
 */
export function Celda({ mano, accion, mix, acciones, resaltada, onTocar }: Props) {
  const colorDe = (clave: string) =>
    acciones.find((a) => a.clave === clave)?.color ?? 'var(--desconocido)'

  const esMix = mix !== null && mix.length > 1

  const fondo = esMix
    ? `linear-gradient(to right, ${tramos(mix, colorDe)})`
    : (accion?.color ?? 'var(--desconocido)')

  const titulo = esMix
    ? `${mano}: ${mix.map((p) => `${p.frecuencia}% ${p.accion}`).join(' · ')}`
    : `${mano}: ${accion?.etiqueta ?? 'desconocida'}`

  const clases = `celda${resaltada ? ' celda-resaltada' : ''}${esMix ? ' celda-mixta' : ''}`
  const estilo = { background: fondo, color: accion?.colorTexto ?? 'var(--texto)' }

  // Solo es interactiva cuando hay editor: una grilla de 169 botones sin nada
  // que hacer estorba al navegar con teclado.
  if (!onTocar) return <div className={clases} style={estilo} title={titulo}>{mano}</div>

  return (
    <button
      type="button"
      className={`${clases} celda-editable`}
      style={estilo}
      title={`${titulo} - toca para corregir`}
      onClick={() => onTocar(mano)}
    >
      {mano}
    </button>
  )
}

/**
 * Convierte las partes en paradas de gradiente duras, para que se vean bandas
 * netas en lugar de un degradado difuso: la celda tiene que leerse de un
 * vistazo, no interpretarse.
 */
function tramos(mix: ParteDeMix[], colorDe: (clave: string) => string): string {
  const total = mix.reduce((suma, p) => suma + p.frecuencia, 0) || 100
  let acumulado = 0
  return mix.flatMap((parte) => {
    const desde = (acumulado / total) * 100
    acumulado += parte.frecuencia
    const hasta = (acumulado / total) * 100
    const color = colorDe(parte.accion)
    return [`${color} ${desde}%`, `${color} ${hasta}%`]
  }).join(', ')
}
