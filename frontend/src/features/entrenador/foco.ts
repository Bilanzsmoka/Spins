/**
 * Qué entrenar cuando se llega desde otra pantalla.
 *
 * Vive en su propio archivo porque lo comparten tres: quien lo anota —el plan
 * del día y la pantalla de rendimiento—, el armazón que lleva de un módulo a
 * otro, y el entrenador que arranca con el filtro puesto.
 */
export interface FocoDeEntrenamiento {
  situacion: string
  /** Nulo cuando sólo se apunta a la tabla entera, sin un spot en particular. */
  spot?: string | null
}
