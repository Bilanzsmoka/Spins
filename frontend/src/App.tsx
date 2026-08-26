import { useEstadoDeVoz } from './core/hooks/useEstadoDeVoz'
import { useEventosDeVoz } from './core/hooks/useEventosDeVoz'
import { ControlDeVoz } from './features/tablas/ControlDeVoz'
import { PaginaDeTablas } from './features/tablas/PaginaDeTablas'
import { Aplicacion, type Modulo } from './shared/Aplicacion'

export default function App() {
  // El estado de voz vive acá porque lo comparten dos zonas: el interruptor
  // en el menú y la página de entrenamiento. Un solo sondeo, una sola
  // suscripción SSE.
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

  return (
    <Aplicacion
      modulos={modulos}
      panelLateral={
        <ControlDeVoz
          disponible={estado?.escuchando ?? conectado}
          activo={estado?.activo ?? false}
          cambiando={cambiando}
          falla={estado?.falla ?? null}
          fallaAlHablar={estado?.fallaAlHablar ?? null}
          errorAlCambiar={errorAlCambiar}
          onAlternar={() => { void alternar() }}
        />
      }
    />
  )
}
