import type { AccionDefinida, ParteDeMix } from '../../core/models/catalogo.model'

interface Props {
  mano: string
  accion: AccionDefinida | undefined
  mix: ParteDeMix[] | null
  acciones: AccionDefinida[]
  resaltada: boolean
}

/**
 * Una celda de la matriz. El color nunca es la única señal: la etiqueta de la
 * mano va siempre visible encima.
 *
 * Una mano mixta se pinta partida en diagonal con los colores de sus partes,
 * en proporción a sus frecuencias — que es como la muestran las tablas de las
 * que salen estos datos, y por lo tanto lo que el usuario ya reconoce.
 */
export function Celda({ mano, accion, mix, acciones, resaltada }: Props) {
  const colorDe = (clave: string) =>
    acciones.find((a) => a.clave === clave)?.color ?? 'var(--desconocido)'

  const esMix = mix !== null && mix.length > 1

  const fondo = esMix
    ? `linear-gradient(135deg, ${tramos(mix, colorDe)})`
    : (accion?.color ?? 'var(--desconocido)')

  const titulo = esMix
    ? `${mano}: ${mix.map((p) => `${p.frecuencia}% ${p.accion}`).join(' · ')}`
    : `${mano}: ${accion?.etiqueta ?? 'desconocida'}`

  return (
    <div
      className={`celda${resaltada ? ' celda-resaltada' : ''}${esMix ? ' celda-mixta' : ''}`}
      style={{ background: fondo, color: accion?.colorTexto ?? 'var(--texto)' }}
      title={titulo}
    >
      {mano}
    </div>
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
