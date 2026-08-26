using Microsoft.EntityFrameworkCore;
using PokerProOS.Application.Diario;
using PokerProOS.Domain.Diario;

namespace PokerProOS.Infrastructure.Database;

public sealed class RepositorioDeDiario(PokerProOSDbContext contexto) : IRepositorioDeDiario
{
    public Task<EntradaDeDiario?> ObtenerAsync(DateOnly fecha, CancellationToken ct) =>
        contexto.EntradasDeDiario.FirstOrDefaultAsync(e => e.Fecha == fecha, ct);

    public async Task<IReadOnlyList<EntradaDeDiario>> ListarAsync(int limite, CancellationToken ct) =>
        await contexto.EntradasDeDiario
            .OrderByDescending(e => e.Fecha)
            .Take(limite)
            .ToListAsync(ct);

    public async Task<EntradaDeDiario> GuardarAsync(EntradaDeDiario entrada, CancellationToken ct)
    {
        var existente = await ObtenerAsync(entrada.Fecha, ct);
        if (existente is null)
        {
            entrada.CreadaEn = DateTime.UtcNow;
            entrada.ActualizadaEn = entrada.CreadaEn;
            contexto.EntradasDeDiario.Add(entrada);
            await contexto.SaveChangesAsync(ct);
            return entrada;
        }

        existente.Intencion = entrada.Intencion;
        existente.NivelDeJuego = entrada.NivelDeJuego;
        existente.Disparador = entrada.Disparador;
        existente.ObjetivoTecnico = entrada.ObjetivoTecnico;
        existente.CumplimientoObjetivo = entrada.CumplimientoObjetivo;
        existente.Mesas = entrada.Mesas;
        existente.Minutos = entrada.Minutos;
        existente.Notas = entrada.Notas;
        existente.ActualizadaEn = DateTime.UtcNow;
        await contexto.SaveChangesAsync(ct);
        return existente;
    }

    public async Task<IReadOnlyDictionary<string, int>> MarcasAsync(DateOnly fecha, CancellationToken ct)
        => await contexto.MarcasDeHabito
            .Where(m => m.Fecha == fecha)
            .ToDictionaryAsync(m => m.Clave, m => m.Valor, ct);

    public async Task<IReadOnlyDictionary<string, string>> NotasDeHabitosAsync(
        DateOnly fecha, CancellationToken ct)
        => await contexto.MarcasDeHabito
            .Where(m => m.Fecha == fecha && m.Nota != null && m.Nota != "")
            .ToDictionaryAsync(m => m.Clave, m => m.Nota!, ct);

    public async Task GuardarMarcasAsync(
        DateOnly fecha,
        IReadOnlyDictionary<string, int> marcas,
        IReadOnlyDictionary<string, string> notas,
        CancellationToken ct)
    {
        var existentes = await contexto.MarcasDeHabito
            .Where(m => m.Fecha == fecha)
            .ToListAsync(ct);

        foreach (var (clave, valor) in marcas)
        {
            var nota = notas.GetValueOrDefault(clave)?.Trim();
            if (string.IsNullOrEmpty(nota)) nota = null;

            var existente = existentes.FirstOrDefault(m => m.Clave == clave);
            if (existente is not null)
            {
                existente.Valor = valor;
                existente.Nota = nota;
            }
            else contexto.MarcasDeHabito.Add(new MarcaDeHabito
            {
                Fecha = fecha, Clave = clave, Valor = valor, Nota = nota
            });
        }

        // Un habito que dejo de venir en el envio se borra: es como se
        // desmarca desde la interfaz.
        foreach (var sobrante in existentes.Where(m => !marcas.ContainsKey(m.Clave)))
            contexto.MarcasDeHabito.Remove(sobrante);

        await contexto.SaveChangesAsync(ct);
    }

    public async Task<Comparativa> CompararAsync(DateOnly fecha, CancellationToken ct)
    {
        // El dia previo con entrada, no necesariamente ayer: si no jugo el
        // sabado, el domingo debe comparar contra el viernes.
        var previa = await contexto.EntradasDeDiario
            .Where(e => e.Fecha < fecha)
            .OrderByDescending(e => e.Fecha)
            .FirstOrDefaultAsync(ct);

        async Task<int?> VolumenDe(DateOnly? dia) => dia is null ? null : await contexto.MarcasDeHabito
            .Where(m => m.Fecha == dia && m.Clave == "VOLUMEN")
            .Select(m => (int?)m.Valor)
            .FirstOrDefaultAsync(ct);

        async Task<int> ConsultasDe(DateOnly? dia)
        {
            if (dia is null) return 0;
            var desde = dia.Value.ToDateTime(TimeOnly.MinValue).ToUniversalTime();
            return await contexto.ConsultasDeVoz.CountAsync(
                c => c.CreadaEn >= desde && c.CreadaEn < desde.AddDays(1), ct);
        }

        return new Comparativa(
            previa?.Fecha,
            previa?.ObjetivoTecnico,
            previa?.CumplimientoObjetivo,
            previa?.NivelDeJuego,
            await VolumenDe(previa?.Fecha),
            await VolumenDe(fecha),
            await ConsultasDe(previa?.Fecha),
            await ConsultasDe(fecha));
    }

    public async Task<ProgresoDeHabitos> ProgresoAsync(
        DateOnly desde, DateOnly hasta, CancellationToken ct)
    {
        var marcas = await contexto.MarcasDeHabito
            .Where(m => m.Fecha >= desde && m.Fecha <= hasta)
            .ToListAsync(ct);

        var niveles = await contexto.EntradasDeDiario
            .Where(e => e.Fecha >= desde && e.Fecha <= hasta)
            .ToDictionaryAsync(e => e.Fecha, e => e.NivelDeJuego, ct);

        var dias = new List<DiaDeGrilla>();
        for (var dia = desde; dia <= hasta; dia = dia.AddDays(1))
        {
            var delDia = marcas.Where(m => m.Fecha == dia).ToList();
            dias.Add(new DiaDeGrilla(
                dia,
                niveles.GetValueOrDefault(dia),
                delDia.ToDictionary(m => m.Clave, m => m.Valor),
                delDia.Where(m => !string.IsNullOrEmpty(m.Nota))
                      .ToDictionary(m => m.Clave, m => m.Nota!)));
        }

        var claves = marcas.Select(m => m.Clave).Distinct().ToList();

        var resumen = claves.Select(clave =>
        {
            var suyas = marcas.Where(m => m.Clave == clave).ToList();
            // Un dia "cumplido" es valor positivo. Para el volumen eso significa
            // haber jugado algo; para los binarios, haber marcado que si.
            var cumplidos = suyas.Count(m => m.Valor > 0);
            var (actual, mejor) = Rachas(dias, clave);
            return new ResumenDeHabito(clave, cumplidos, suyas.Count(m => m.Valor != 0), actual, mejor);
        }).ToList();

        var cruces = claves.Select(clave => Cruzar(clave, dias)).ToList();

        return new ProgresoDeHabitos(desde, hasta, dias, resumen, cruces);
    }

    /// <summary>
    /// Racha de dias consecutivos cumpliendo. Un dia sin marcar no rompe la
    /// racha: no marcar no es lo mismo que no haberlo hecho, y castigarlo
    /// haria que el usuario deje de abrir la aplicacion los dias flojos.
    /// </summary>
    private static (int Actual, int Mejor) Rachas(IReadOnlyList<DiaDeGrilla> dias, string clave)
    {
        var actual = 0;
        var mejor = 0;
        foreach (var dia in dias)
        {
            if (!dia.Marcas.TryGetValue(clave, out var valor) || valor == 0) continue;
            if (valor > 0) { actual++; mejor = Math.Max(mejor, actual); }
            else actual = 0;
        }
        return (actual, mejor);
    }

    /// <summary>
    /// Cruza el habito contra como se jugo. "Bueno" es A o B; C no lo es.
    /// Solo cuentan los dias que tienen las dos cosas anotadas.
    /// </summary>
    private static CruceDeHabito Cruzar(string clave, IReadOnlyList<DiaDeGrilla> dias)
    {
        const int MinimoParaConfiar = 4;

        var conNivel = dias
            .Where(d => d.NivelDeJuego is "A" or "B" or "C")
            .Where(d => d.Marcas.TryGetValue(clave, out var v) && v != 0)
            .ToList();

        var con = conNivel.Where(d => d.Marcas[clave] > 0).ToList();
        var sin = conNivel.Where(d => d.Marcas[clave] < 0).ToList();

        static int Buenos(IEnumerable<DiaDeGrilla> dias) =>
            dias.Count(d => d.NivelDeJuego is "A" or "B");

        return new CruceDeHabito(
            clave,
            con.Count, Buenos(con),
            sin.Count, Buenos(sin),
            con.Count >= MinimoParaConfiar && sin.Count >= MinimoParaConfiar);
    }

    /// <summary>
    /// Lo que ningún tracker tiene: qué manos consultó ese día. Un tracker ve
    /// las manos que jugó; esto ve las que no sabía.
    /// </summary>
    public async Task<ResumenDelDia> ResumirAsync(DateOnly fecha, CancellationToken ct)
    {
        var desde = fecha.ToDateTime(TimeOnly.MinValue).ToUniversalTime();
        var hasta = desde.AddDays(1);

        var consultas = await contexto.ConsultasDeVoz
            .Where(c => c.CreadaEn >= desde && c.CreadaEn < hasta)
            .ToListAsync(ct);

        if (consultas.Count == 0)
            return new ResumenDelDia(0, 0, [], null, null);

        var top = consultas
            .Where(c => c.Resuelta && c.Mano.Length > 0)
            .GroupBy(c => new { c.Mano, c.Accion })
            .OrderByDescending(g => g.Count())
            .ThenBy(g => g.Key.Mano)
            .Take(8)
            .Select(g => new ManoConsultada(g.Key.Mano, g.Key.Accion, g.Count()))
            .ToList();

        static string Hora(DateTime utc) => utc.ToLocalTime().ToString("HH:mm");

        return new ResumenDelDia(
            consultas.Count,
            consultas.Count(c => c.Resuelta),
            top,
            Hora(consultas.Min(c => c.CreadaEn)),
            Hora(consultas.Max(c => c.CreadaEn)));
    }
}
