export interface PropsDeVoz {
  /** El motor arrancó bien y hay algo que encender. */
  disponible: boolean
  /** El usuario lo tiene encendido ahora. */
  activo: boolean
  /**
   * El motor está oyendo AHORA. No es lo mismo que <c>activo</c>: Chrome corta
   * la escucha sola cada tanto, y un permiso denegado o un micrófono ocupado
   * la matan del todo. Decir "Escuchando" mirando el interruptor era la peor
   * mentira posible acá — hablabas creyendo que te oía.
   */
  escuchando: boolean
  /** Lo último que hizo el motor, tal cual. Para poder decir qué pasa. */
  ultimoEvento: string | null
  cambiando: boolean
  falla: string | null
  fallaAlHablar: string | null
  errorAlCambiar: string | null
  onAlternar: () => void
}

/**
 * El interruptor del copiloto. Se apaga entre sesiones para que la
 * aplicación no conteste sola mientras no se está jugando.
 */
export function ControlDeVoz({
  disponible, activo, escuchando, ultimoEvento, cambiando, falla, fallaAlHablar,
  errorAlCambiar, onAlternar,
}: PropsDeVoz) {
  const estado = !disponible ? 'sin-motor'
    : escuchando ? 'activo'
      : activo ? 'reenganchando'
        : 'apagado'
  const texto = {
    'sin-motor': 'Sin micrófono',
    activo: 'Escuchando',
    // Encendido pero el motor no está oyendo: o Chrome cortó y estamos
    // reenganchando —cosa de un instante— o algo lo tiene tomado.
    reenganchando: 'Reintentando…',
    apagado: 'Apagado',
  }[estado]

  return (
    <section className="control-voz" aria-live="polite">
      <div className="control-voz-cabecera">
        <span className={`punto punto-${estado}`} aria-hidden="true" />
        <span className="control-voz-estado">{texto}</span>
      </div>

      <button
        type="button"
        className={`boton-voz${activo ? ' boton-voz-activo' : ''}`}
        disabled={!disponible || cambiando}
        onClick={onAlternar}
      >
        {cambiando ? '…' : activo ? 'Apagar voz' : 'Encender voz'}
      </button>

      {/* El reconocedor no arrancó: distinto de una respuesta muda puntual. */}
      {falla && <p className="aviso-voz">No se pudo iniciar el reconocedor: {falla}</p>}
      {/* La síntesis falló en la última respuesta, pero se sigue escuchando. */}
      {fallaAlHablar && <p className="aviso-voz">No se pudo hablar la última respuesta.</p>}
      {errorAlCambiar && <p className="aviso-voz">{errorAlCambiar}</p>}

      {/* Sin esto, "no me escucha" es indistinguible de "nadie habló": los dos
          se ven igual en pantalla y no hay por dónde empezar a mirar. */}
      {activo && ultimoEvento && (
        <p className="detalle-voz">Micrófono: {ultimoEvento}</p>
      )}
    </section>
  )
}
