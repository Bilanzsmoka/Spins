using Microsoft.EntityFrameworkCore;
using PokerProOS.Application.Tablas;
using PokerProOS.Domain.Entities;

namespace PokerProOS.Infrastructure.Database;

/// <summary>
/// Vuelca el catálogo validado a la base. Los JSON son la fuente de verdad;
/// esto es solo el espejo consultable para cruces relacionales.
/// </summary>
public sealed class SincronizadorDeCatalogo(PokerProOSDbContext contexto)
{
    public async Task<int> SincronizarAsync(ICatalogoDeTablas catalogo, CancellationToken cancelacion)
    {
        var celdas = new List<ChartStrategyCell>();

        foreach (var situacion in catalogo.Situaciones)
            foreach (var tabla in situacion.Stacks)
                foreach (var spot in tabla.Spots)
                    foreach (var celda in spot.Celdas)
                        celdas.Add(new ChartStrategyCell
                        {
                            SituationKey = situacion.Clave,
                            SituationLabel = situacion.Etiqueta,
                            StackKey = tabla.Stack.Clave,
                            MinBB = tabla.Stack.MinBB,
                            MaxBB = tabla.Stack.MaxBB,
                            SpotKey = spot.Clave,
                            SpotLabel = spot.Etiqueta,
                            HandLabel = celda.Mano,
                            Action = celda.Accion,
                            Source = "json",
                            Version = "v1",
                            UpdatedAt = DateTime.UtcNow
                        });

        // Reemplazo completo: los JSON mandan, lo que haya en la base sobra.
        // ExecuteDeleteAsync no esta soportado por el proveedor en memoria
        // (usado en las pruebas), asi que se distingue por Database.IsRelational().
        if (contexto.Database.IsRelational())
            await contexto.ChartStrategyCells.ExecuteDeleteAsync(cancelacion);
        else
            contexto.ChartStrategyCells.RemoveRange(contexto.ChartStrategyCells);

        contexto.ChartStrategyCells.AddRange(celdas);
        await contexto.SaveChangesAsync(cancelacion);
        return celdas.Count;
    }
}
