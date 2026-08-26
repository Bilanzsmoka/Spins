import { useEstadoDeVoz } from './core/hooks/useEstadoDeVoz'
import { useEventosDeVoz } from './core/hooks/useEventosDeVoz'
import { PaginaDeTablas } from './features/tablas/PaginaDeTablas'
import { Aplicacion, type Modulo } from './shared/Aplicacion'

export default function App() {
  // El estado de voz se resuelve acá, no dentro de la página, para que el
  // sondeo y la suscripción SSE sobrevivan al cambio de módulo: si el
  // usuario mira otra pantalla un momento, el copiloto no se reinicia.
  const { estado, alternar, cambiando, errorAlCambiar } = useEstadoDeVoz()
  const { ultimo, historial, conectado, limpiarHistorial } = useEventosDeVoz()

  const modulos: Modulo[] = [
    {
      clave: 'entrenamiento',
      etiqueta: 'Entrenamiento',
      descripcion: 'Tablas preflop y copiloto',
      disponible: true,
      contenido: (
        <PaginaDeTablas
          ultimo={ultimo}
          historial={historial}
          onLimpiarHistorial={limpiarHistorial}
          voz={{
            disponible: estado?.escuchando ?? conectado,
            activo: estado?.activo ?? false,
            cambiando,
            falla: estado?.falla ?? null,
            fallaAlHablar: estado?.fallaAlHablar ?? null,
            errorAlCambiar,
            onAlternar: () => { void alternar() },
          }}
        />
      ),
    },
    {
      clave: 'spins',
      etiqueta: 'Spins',
      descripcion: 'Sesiones y resultados',
      disponible: false,
    },
    {
      clave: 'bankroll',
      etiqueta: 'Bankroll',
      descripcion: 'Movimientos y salas',
      disponible: false,
    },
  ]

  return <Aplicacion modulos={modulos} />
}
