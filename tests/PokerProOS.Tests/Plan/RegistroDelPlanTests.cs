using PokerProOS.Application.Plan;
using PokerProOS.Infrastructure.Diario;
using PokerProOS.Infrastructure.Plan;
using PokerProOS.Infrastructure.Tablas;

namespace PokerProOS.Tests.Plan;

/// <summary>
/// El plan real, contra las tablas y los hábitos reales.
///
/// Un hito que apunta a una situación que ya no existe no explota: se muestra
/// como problema y se queda ahí para siempre, esperando un avance imposible.
/// Por eso el que se controla es el archivo, no el código que lo lee.
/// </summary>
public class RegistroDelPlanTests
{
    private static PlanDefinido Plan() =>
        RegistroDelPlanJson.Cargar(Rutas.Registro("plan.json")).Plan;

    [Fact]
    public void El_plan_real_carga_con_hitos()
    {
        var plan = Plan();

        Assert.True(plan.HayPlan);
        Assert.True(plan.MetaDeVolumen > 0);
        Assert.All(plan.Hitos, h => Assert.False(string.IsNullOrWhiteSpace(h.Titulo), h.Clave));
    }

    [Fact]
    public void Cada_hito_de_saber_apunta_a_una_situacion_que_existe()
    {
        var acciones = RegistroDeAccionesJson.Cargar(Rutas.Registro("acciones.json"));
        var catalogo = new CargadorDeTablas(new ValidadorDeTabla(acciones), acciones)
            .CargarDirectorio(Rutas.SemillasDeTablas);

        foreach (var hito in Plan().Hitos.Where(h => h.Tipo == "saber"))
            Assert.True(
                catalogo.Situacion(hito.Situacion!) is not null,
                $"El hito «{hito.Clave}» apunta a la situación «{hito.Situacion}», que no existe.");
    }

    [Fact]
    public void Cada_hito_de_jugar_apunta_a_un_habito_que_existe()
    {
        var habitos = RegistroDeHabitosJson.Cargar(Rutas.Registro("habitos.json"));
        var plan = Plan();

        foreach (var hito in plan.Hitos.Where(h => h.Tipo == "jugar"))
            Assert.True(habitos.Existe(hito.Habito!), $"No existe el hábito «{hito.Habito}».");

        Assert.True(habitos.Existe(plan.HabitoDeVolumen), plan.HabitoDeVolumen);
        Assert.True(habitos.Existe(plan.HabitoDeEstudio), plan.HabitoDeEstudio);
    }

    /// <summary>
    /// Sólo hay dos tipos. Uno mal escrito en el JSON no rompe nada visible
    /// —el medidor lo reporta— pero deja un hito que nunca avanza.
    /// </summary>
    [Fact]
    public void Ningun_hito_tiene_un_tipo_inventado()
        => Assert.All(Plan().Hitos, h => Assert.Contains(h.Tipo, new[] { "saber", "jugar" }));

    /// <summary>Sin archivo no hay plan, y eso no es un error: es que no lo escribiste.</summary>
    [Fact]
    public void Sin_archivo_el_plan_queda_vacio()
    {
        var plan = RegistroDelPlanJson.Cargar("no-existe-este-archivo.json").Plan;

        Assert.False(plan.HayPlan);
    }
}
