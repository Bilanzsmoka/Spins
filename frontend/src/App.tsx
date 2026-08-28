import { useEventosDeVoz } from './core/hooks/useEventosDeVoz'
import { useVozDelNavegador } from './core/hooks/useVozDelNavegador'
import { PaginaDeDiario } from './features/diario/PaginaDeDiario'
import { PaginaDeHabitos } from './features/diario/PaginaDeHabitos'
import { PaginaDeTablas } from './features/tablas/PaginaDeTablas'
import { PaginaDeVocabulario } from './features/voz/PaginaDeVocabulario'
import { Aplicacion, type GrupoDeModulos } from './shared/Aplicacion'

export default function App() {
  // El estado de voz se resuelve acá, no dentro de la página, para que la
  // suscripción SSE sobreviva al cambio de módulo: si el usuario mira otra
  // pantalla un momento, el copiloto no se reinicia.
  const { ultimo, historial, limpiarHistorial } = useEventosDeVoz()
  // El navegador oye y habla directo, sin pasar por el reconocedor del
  // servidor: la respuesta que llega por SSE es lo que hay que decir en voz.
  const { disponible, activo, falla, fallaAlHablar, alternar, capturar } =
    useVozDelNavegador(ultimo ?? null)

  const grupos: GrupoDeModulos[] = [
    {
      clave: 'spins',
      etiqueta: 'Spins',
      modulos: [
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
                disponible,
                activo,
                // El toggle del navegador es sincrónico: no hay un pedido en
                // vuelo que mostrar como "cambiando".
                cambiando: false,
                falla,
                fallaAlHablar,
                errorAlCambiar: null,
                onAlternar: alternar,
              }}
            />
          ),
        },
        {
          clave: 'diario',
          etiqueta: 'Diario',
          descripcion: 'Tu día y tu evolución',
          disponible: true,
          contenido: <PaginaDeDiario />,
        },
        {
          clave: 'habitos',
          etiqueta: 'Hábitos',
          descripcion: 'Cumplimiento y efecto',
          disponible: true,
          contenido: <PaginaDeHabitos />,
        },
        {
          clave: 'sesiones',
          etiqueta: 'Sesiones',
          descripcion: 'Volumen y resultados',
          disponible: false,
        },
      ],
    },
    {
      clave: 'ajustes',
      etiqueta: 'Ajustes',
      modulos: [
        {
          clave: 'voz',
          etiqueta: 'Voz',
          descripcion: 'Como decis vos cada cosa',
          disponible: true,
          // La captura sale del mismo hook que escucha: el micrófono es uno
          // solo y así el motor continuo queda pausado mientras se graba.
          contenido: <PaginaDeVocabulario onCapturar={capturar} />,
        },
      ],
    },
    {
      clave: 'banca',
      etiqueta: 'Banca',
      modulos: [
        {
          clave: 'bankroll',
          etiqueta: 'Bankroll',
          descripcion: 'Movimientos y salas',
          disponible: false,
        },
      ],
    },
  ]

  return <Aplicacion grupos={grupos} />
}
