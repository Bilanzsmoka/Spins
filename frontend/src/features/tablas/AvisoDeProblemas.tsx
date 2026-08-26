import type { ProblemaDeTabla } from '../../core/models/catalogo.model'

export function AvisoDeProblemas({ problemas }: { problemas: ProblemaDeTabla[] }) {
  if (problemas.length === 0) return null
  return (
    <section className="aviso-problemas">
      <strong>{problemas.length} tabla(s) con problemas. El resto se cargó igual.</strong>
      <ul>
        {problemas.map((p, i) => (
          <li key={i}>
            <code>{p.archivo}</code> {p.stack}/{p.spot}: {p.mensaje}
          </li>
        ))}
      </ul>
    </section>
  )
}
