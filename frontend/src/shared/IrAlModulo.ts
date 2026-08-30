import { createContext } from 'react'

/**
 * Cambiar de módulo desde adentro de uno.
 *
 * El dueño del módulo activo es el armazón, así que el contexto lo provee él.
 * Lo usa el panel del día para mandarte a entrenar la tabla que toca: un plan
 * que te dice qué hacer y no te lleva es media cosa.
 *
 * Vive en su propio archivo y no dentro de Aplicacion.tsx porque un módulo que
 * exporta un componente y además otra cosa rompe el refresco en caliente.
 */
export const IrAlModulo = createContext<(clave: string) => void>(() => {})
