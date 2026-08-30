using PokerProOS.Application.Diario;
using PokerProOS.Application.Tablas;
using PokerProOS.Domain.Entrenador;

namespace PokerProOS.Application.Plan;

/// <summary>
/// Contesta la única pregunta que importa todos los días: ¿hoy voy bien?
///
/// Puro a propósito —recibe el catálogo, el progreso y las marcas ya cargados,
/// y el día entra como parámetro—, igual que <c>CalendarioDeRepeticion</c>.
/// Así la regla se prueba entera sin base y sin depender del día en que se
/// corren las pruebas.
/// </summary>
public static class MedidorDeHitos
{
    private const int DiasDeLaTira = 7;

    public static EstadoDelDia Medir(
        PlanDefinido plan,
        ICatalogoDeTablas catalogo,
        IRegistroDeHabitos habitos,
        IReadOnlyList<ProgresoDeCasilla> progreso,
        IReadOnlyList<DiaDeGrilla> dias,
        DateOnly hoy)
    {
        var porFecha = dias
            .GroupBy(d => d.Fecha)
            .ToDictionary(g => g.Key, g => g.Last());

        // El intervalo concedido a cada casilla. Es lo que dice cuánto se sabe:
        // 16 días son cuatro aciertos separados en el tiempo.
        var intervalos = new Dictionary<string, int>();
        foreach (var p in progreso) intervalos[p.ClaveDeCasilla()] = p.IntervaloEnDias;

        var medidos = plan.Hitos
            .Select(h => Medir(h, catalogo, habitos, intervalos, porFecha, plan, hoy))
            .ToList();

        // El activo es el primero sin cumplir que además se pueda medir: un
        // hito roto no puede ser lo que estás haciendo hoy.
        var activo = medidos.FirstOrDefault(h => h.Problema is null && !h.Cumplido);
        var hitos = medidos
            .Select(h => h with { EsElActivo = ReferenceEquals(h, activo) })
            .ToList();

        var semana = Tira(plan, porFecha, hoy);

        return new EstadoDelDia(
            plan.MetaDeVolumen,
            Marca(porFecha, hoy, plan.HabitoDeVolumen),
            Marca(porFecha, hoy, plan.HabitoDeEstudio) > 0,
            hitos,
            semana,
            SinDosSeguidos(semana, hoy),
            medidos.FirstOrDefault(h => h.Problema is null && !h.Cumplido && h.Situacion is not null)
                ?.Situacion);
    }

    private static EstadoDeHito Medir(
        HitoDefinido hito,
        ICatalogoDeTablas catalogo,
        IRegistroDeHabitos habitos,
        IReadOnlyDictionary<string, int> intervalos,
        IReadOnlyDictionary<DateOnly, DiaDeGrilla> porFecha,
        PlanDefinido plan,
        DateOnly hoy) => hito.Tipo.ToLowerInvariant() switch
        {
            "saber" => Saber(hito, catalogo, intervalos),
            "jugar" => Jugar(hito, habitos, porFecha, hoy),
            _ => Roto(hito, $"No entiendo el tipo «{hito.Tipo}»: sólo hay «saber» y «jugar»."),
        };

    /// <summary>
    /// Una tabla sabida. El denominador son sus <b>bordes</b> y no sus 169
    /// casillas: el borde es donde se corta el bloque —lo que separa saber de
    /// adivinar— y el interior se sabe sabiendo dónde termina. Contar las 169
    /// haría el hito cinco veces más largo sin medir nada más.
    ///
    /// Y no son las casillas contestadas: si el denominador fuera ésas,
    /// estudiar diez y acertarlas daría 100%.
    /// </summary>
    private static EstadoDeHito Saber(
        HitoDefinido hito,
        ICatalogoDeTablas catalogo,
        IReadOnlyDictionary<string, int> intervalos)
    {
        if (hito.Situacion is not { Length: > 0 } clave)
            return Roto(hito, "Un hito de saber tiene que decir a qué situación apunta.");

        var situacion = catalogo.Situacion(clave);
        if (situacion is null)
            return Roto(hito, $"No existe la situación «{clave}».");

        var total = 0;
        var hecho = 0;

        foreach (var tabla in situacion.Stacks)
            foreach (var spot in tabla.Spots)
                foreach (var celda in spot.Celdas)
                {
                    if (!spot.EnElBorde(celda.Mano)) continue;
                    total++;

                    var casilla = ProgresoDeCasilla.Clave(
                        situacion.Clave, tabla.Stack.Clave, spot.Clave, celda.Mano);
                    if (intervalos.TryGetValue(casilla, out var dias) && dias >= hito.EscalonMinimo)
                        hecho++;
                }

        return Armar(hito, hecho, total, situacion.Clave);
    }

    /// <summary>
    /// Volumen sostenido. Se mira la ventana de los últimos días y se cumple
    /// si nunca hubo <b>dos días seguidos</b> por debajo del objetivo — no si
    /// la racha está intacta. Medir días seguidos hace largar el hábito entero
    /// al primer fallo; la regla de los dos días es la que se sostiene.
    ///
    /// El día de hoy no puede fallar: todavía no terminó.
    /// </summary>
    private static EstadoDeHito Jugar(
        HitoDefinido hito,
        IRegistroDeHabitos habitos,
        IReadOnlyDictionary<DateOnly, DiaDeGrilla> porFecha,
        DateOnly hoy)
    {
        if (hito.Habito is not { Length: > 0 } habito)
            return Roto(hito, "Un hito de jugar tiene que decir qué hábito mide.");
        if (!habitos.Existe(habito))
            return Roto(hito, $"No existe el hábito «{habito}».");
        if (hito.Dias <= 0)
            return Roto(hito, "Un hito de jugar tiene que decir cuántos días mira.");

        var alcanzados = 0;
        var fallosSeguidos = 0;
        var hubieronDos = false;

        for (var atras = hito.Dias - 1; atras >= 0; atras--)
        {
            var fecha = hoy.AddDays(-atras);
            var alcanzo = Marca(porFecha, fecha, habito) >= hito.Objetivo;
            if (alcanzo) alcanzados++;

            if (fecha == hoy) continue;

            fallosSeguidos = alcanzo ? 0 : fallosSeguidos + 1;
            if (fallosSeguidos >= 2) hubieronDos = true;
        }

        var estado = Armar(hito, alcanzados, hito.Dias, null);
        return estado with { Cumplido = !hubieronDos && alcanzados > 0 };
    }

    private static EstadoDeHito Armar(HitoDefinido hito, int hecho, int total, string? situacion)
    {
        // Truncado y no redondeado: 89,6% no puede mostrarse como un 90 cumplido.
        var porcentaje = total == 0 ? 0 : (int)(100.0 * hecho / total);
        return new EstadoDeHito(
            hito.Clave, hito.Titulo, hito.Tipo,
            hecho, total, porcentaje, hito.Objetivo,
            Cumplido: porcentaje >= hito.Objetivo,
            EsElActivo: false,
            Situacion: situacion);
    }

    private static EstadoDeHito Roto(HitoDefinido hito, string causa) => new(
        hito.Clave, hito.Titulo, hito.Tipo,
        0, 0, 0, hito.Objetivo,
        Cumplido: false, EsElActivo: false, Situacion: null, Problema: causa);

    private static IReadOnlyList<DiaDelPlan> Tira(
        PlanDefinido plan, IReadOnlyDictionary<DateOnly, DiaDeGrilla> porFecha, DateOnly hoy)
    {
        var tira = new List<DiaDelPlan>(DiasDeLaTira);
        for (var atras = DiasDeLaTira - 1; atras >= 0; atras--)
        {
            var fecha = hoy.AddDays(-atras);
            var volumen = Marca(porFecha, fecha, plan.HabitoDeVolumen);
            tira.Add(new DiaDelPlan(
                fecha, volumen, volumen >= plan.MetaDeVolumen && plan.MetaDeVolumen > 0, fecha == hoy));
        }
        return tira;
    }

    /// <summary>Hoy queda afuera: un día que todavía no terminó no falló.</summary>
    private static bool SinDosSeguidos(IReadOnlyList<DiaDelPlan> semana, DateOnly hoy)
    {
        var seguidos = 0;
        foreach (var dia in semana)
        {
            if (dia.Fecha == hoy) continue;
            seguidos = dia.Alcanzo ? 0 : seguidos + 1;
            if (seguidos >= 2) return false;
        }
        return true;
    }

    private static int Marca(
        IReadOnlyDictionary<DateOnly, DiaDeGrilla> porFecha, DateOnly fecha, string habito)
        => habito.Length > 0
           && porFecha.TryGetValue(fecha, out var dia)
           && dia.Marcas.TryGetValue(habito, out var valor)
            ? valor
            : 0;
}
