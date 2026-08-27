using PokerProOS.Application.Tablas;
using PokerProOS.Domain.Manos;
using PokerProOS.Domain.Tablas;
using PokerProOS.Infrastructure.Tablas;

namespace PokerProOS.Tests.Tablas;

public class AnalizadorDeMemoriaTests
{
    private static AnalizadorDeMemoria Analizador()
    {
        var acciones = RegistroDeAccionesJson.Cargar(Rutas.Registro("acciones.json"));
        return new(new CargadorDeTablas(new ValidadorDeTabla(acciones), acciones)
            .CargarDirectorio(Rutas.SemillasDeTablas));
    }

    private static FichaDeMemoria Ficha(
        string mano, string stack = "17-18bb", string spot = "SB_OR",
        string situacion = "HU_SB_OR_FISH")
        => Analizador().Analizar(situacion, stack, spot, mano)!;

    /// <summary>
    /// Un catálogo a medida, sin tocar <c>database/</c>: una situación con los
    /// stacks (clave, minBB, maxBB, spots) que se le pasen, cada spot con las
    /// 169 manos apuntando a la misma acción. Sirve para probar reglas del
    /// analizador (fusión de bandas, línea de un paso) sin depender de qué
    /// tablas tenga cargado el usuario en este momento.
    /// </summary>
    private static AnalizadorDeMemoria AnalizadorSintetico(
        string situacion, string accion,
        params (string ClaveDeStack, decimal MinBB, decimal MaxBB, string[] Spots)[] stacks)
    {
        var celdas = MatrizDeManos.Todas()
            .Select(mano => new CeldaDeTabla(mano, accion))
            .ToList();

        var tablas = stacks
            .Select(s => new TablaDeStack(
                new RangoDeStack(s.ClaveDeStack, s.MinBB, s.MaxBB),
                s.Spots.Select(clave => new SpotDeTabla(clave, clave, celdas)).ToList()))
            .ToList();

        var catalogo = new CatalogoEnMemoria(
            [new SituacionDeTabla(situacion, situacion, "HU", tablas)], []);
        return new AnalizadorDeMemoria(catalogo);
    }

    [Fact]
    public void Sin_tip_declarado_la_ficha_no_trae_tip()
        => Assert.Null(Ficha("A8o").Tip);

    [Fact]
    public void Sin_ficha_cuando_el_spot_no_existe()
        => Assert.Null(Analizador().Analizar("HU_SB_OR_FISH", "17-18bb", "NO_EXISTE", "A8o"));

    [Fact]
    public void Sin_ficha_cuando_la_mano_no_existe()
        => Assert.Null(Analizador().Analizar("HU_SB_OR_FISH", "17-18bb", "SB_OR", "XX"));

    [Fact]
    public void Trae_la_accion_de_la_mano()
    {
        var ficha = Ficha("A8o");
        Assert.Equal("A8o", ficha.Mano);
        Assert.Equal("CALL", ficha.Accion);
        Assert.Equal("17-18bb", ficha.ClaveDeStack);
    }

    [Fact]
    public void El_peso_se_mide_en_combos_de_baraja_no_en_casillas()
    {
        var ficha = Ficha("A8o");
        var raise = ficha.Pesos.Single(p => p.Accion == "RAISE_X2");

        // 84 casillas de 169 son 49,7 %, pero lo que importa es la baraja.
        // 660.0, no 660: la sobrecarga con precisión de xUnit es de double.
        Assert.Equal(660.0, raise.Combos, 3);
        Assert.Equal(49.8, raise.PorcentajeDeBaraja, 1);
    }

    [Fact]
    public void Los_pesos_del_spot_suman_la_baraja_entera()
    {
        var ficha = Ficha("A8o");
        Assert.Equal(100.0, ficha.Pesos.Sum(p => p.PorcentajeDeBaraja), 6);
    }

    [Fact]
    public void Los_pesos_vienen_ordenados_de_mayor_a_menor()
    {
        var pesos = Ficha("A8o").Pesos.Select(p => p.Combos).ToList();
        Assert.Equal(pesos.OrderByDescending(c => c).ToList(), pesos);
    }

    [Fact]
    public void El_ancla_dice_donde_se_corta_la_familia()
    {
        var ancla = Ficha("A8o").Ancla!;
        Assert.Equal("Axo", ancla.Familia);
        Assert.Equal("A8o", ancla.Tope);
        Assert.Equal("A2o", ancla.Fondo);
        Assert.Equal("CALL", ancla.Accion);
        // El bloque de CALL llega hasta el final de la familia.
        Assert.Null(ancla.Siguiente);
    }

    [Fact]
    public void El_ancla_de_la_mano_de_arriba_apunta_a_la_que_rompe()
    {
        var ancla = Ficha("AKo").Ancla!;
        Assert.Equal("Axo", ancla.Familia);
        Assert.Equal("AKo", ancla.Tope);
        Assert.Equal("A9o", ancla.Fondo);
        Assert.Equal("RAISE_X2", ancla.Accion);
        Assert.Equal("A8o", ancla.Siguiente);
        Assert.Equal("CALL", ancla.AccionSiguiente);
    }

    [Fact]
    public void El_ancla_de_una_pareja_se_mide_contra_los_pares()
    {
        var ancla = Ficha("77").Ancla!;
        Assert.Equal("Pares", ancla.Familia);
        Assert.Equal("AA", ancla.Tope);
        Assert.Equal("55", ancla.Fondo);
        Assert.Equal("44", ancla.Siguiente);
        Assert.Equal("ALL-IN", ancla.AccionSiguiente);
    }

    [Fact]
    public void Una_familia_entera_de_la_misma_accion_no_tiene_ancla()
    {
        // A 8-8bb el spot no tiene folds y todo Axo es ALL-IN.
        Assert.Null(Ficha("A8o", stack: "8-8bb").Ancla);
    }

    [Fact]
    public void El_umbral_colapsa_los_stacks_que_hacen_lo_mismo()
    {
        var umbral = Ficha("A8o").Umbral;

        Assert.Equal(3, umbral.Count);

        Assert.Equal("ALL-IN", umbral[0].Accion);
        Assert.Equal(1m, umbral[0].MinBB);
        Assert.Equal(16m, umbral[0].MaxBB);

        Assert.Equal("CALL", umbral[1].Accion);
        Assert.Equal(17m, umbral[1].MinBB);
        Assert.Equal(18m, umbral[1].MaxBB);
        Assert.Equal("17-18bb", umbral[1].ClaveDeStack);
        Assert.True(umbral[1].EsElActual);
        Assert.False(umbral[0].EsElActual);
        Assert.False(umbral[2].EsElActual);

        Assert.Equal("RAISE_X2", umbral[2].Accion);
        Assert.Equal(19m, umbral[2].MinBB);
        Assert.Equal(99m, umbral[2].MaxBB);
    }

    [Fact]
    public void Una_banda_de_varios_stacks_nombra_sus_extremos()
    {
        // Extremos por CLAVE, no por número: el último tramo entra con su
        // nombre entero. Nueve stacks (1-4bb … 13-16bb) colapsan en uno.
        Assert.Equal("1-4bb…13-16bb", Ficha("A8o").Umbral[0].ClaveDeStack);
    }

    [Fact]
    public void La_banda_actual_se_marca_aunque_este_fusionada()
    {
        // A 10-10bb, A8o cae adentro de la banda ALL-IN que junta nueve stacks.
        // Comparar claves no serviría: la banda no se llama "10-10bb".
        var umbral = Ficha("A8o", stack: "10-10bb").Umbral;
        var actual = umbral.Single(b => b.EsElActual);
        Assert.Equal("ALL-IN", actual.Accion);
        Assert.Equal("1-4bb…13-16bb", actual.ClaveDeStack);
    }

    [Fact]
    public void El_umbral_de_una_mano_fuerte_igual_se_calcula()
    {
        // AA en HU_SB_OR_FISH / SB_OR corta en tres bandas reales: a stacks
        // muy cortos se shovea, en el medio se paga y desde 13bb para arriba
        // (fusionado con el consultado, 17-18bb) se sube.
        var umbral = Ficha("AA").Umbral;

        Assert.Equal(3, umbral.Count);

        Assert.Equal("ALL-IN", umbral[0].Accion);
        Assert.Equal("1-4bb", umbral[0].ClaveDeStack);
        Assert.Equal(1m, umbral[0].MinBB);
        Assert.Equal(4m, umbral[0].MaxBB);
        Assert.False(umbral[0].EsElActual);

        Assert.Equal("CALL", umbral[1].Accion);
        Assert.Equal("5-5bb…11-12bb", umbral[1].ClaveDeStack);
        Assert.Equal(5m, umbral[1].MinBB);
        Assert.Equal(12m, umbral[1].MaxBB);
        Assert.False(umbral[1].EsElActual);

        Assert.Equal("RAISE_X2", umbral[2].Accion);
        Assert.Equal("13-16bb…19-99bb", umbral[2].ClaveDeStack);
        Assert.Equal(13m, umbral[2].MinBB);
        Assert.Equal(99m, umbral[2].MaxBB);
        Assert.True(umbral[2].EsElActual);
    }

    [Fact]
    public void Las_familias_emparentadas_son_las_dos_del_rango_alto_y_los_pares()
    {
        var familias = Ficha("A8o").Familias;

        // new[] y ToArray(): una expresión de colección como argumento de
        // Assert.Equal no tiene tipo destino y no resuelve la sobrecarga.
        Assert.Equal(new[] { "Axs", "Axo", "Pares" }, familias.Select(f => f.Familia).ToArray());

        var suited = familias.Single(f => f.Familia == "Axs");
        Assert.Equal("AKs", suited.Tope);
        Assert.Equal("A7s", suited.Fondo);
        Assert.Equal("RAISE_X2", suited.Accion);
        Assert.Equal("A6s", suited.Siguiente);

        var offsuit = familias.Single(f => f.Familia == "Axo");
        Assert.Equal("A9o", offsuit.Fondo);

        var pares = familias.Single(f => f.Familia == "Pares");
        Assert.Equal("55", pares.Fondo);
        Assert.Equal("44", pares.Siguiente);
        Assert.Equal("ALL-IN", pares.AccionSiguiente);
    }

    [Fact]
    public void Una_pareja_solo_empareja_con_los_pares()
        => Assert.Equal(new[] { "Pares" }, Ficha("77").Familias.Select(f => f.Familia).ToArray());

    [Fact]
    public void La_linea_recorre_los_spots_del_stack_en_orden()
    {
        var linea = Ficha("A8o").Linea;

        Assert.Equal(
            new[] { "SB_OR", "VS_BB_ALL_IN", "VS_BB_3BET", "VS_BB_ISO_3BB", "VS_BB_ISO_ALL_IN" },
            linea.Select(p => p.Spot).ToArray());
        Assert.Equal("Mi acción · SB OR", linea[0].Etiqueta);
        Assert.True(linea[0].EsElConsultado);
        Assert.All(linea.Skip(1), paso => Assert.False(paso.EsElConsultado));
        Assert.All(linea, paso => Assert.False(string.IsNullOrEmpty(paso.Accion)));
    }

    [Fact]
    public void Un_stack_con_un_solo_spot_da_una_linea_de_un_paso()
    {
        var analizador = AnalizadorSintetico("SIT", "ALL-IN",
            ("1-5bb", 1m, 5m, ["UNICO_SPOT"]));

        var ficha = analizador.Analizar("SIT", "1-5bb", "UNICO_SPOT", "A8o")!;
        Assert.Single(ficha.Linea);
        Assert.True(ficha.Linea[0].EsElConsultado);
    }

    [Fact]
    public void Dos_stacks_con_un_hueco_real_dan_dos_bandas()
    {
        // 0.5-1.5 y 2.5-3.5 son la misma acción, pero entre medio hay un
        // stack (1.5-2.5) que no declara "SPOT" — de ese rango de stack
        // ninguna tabla dice nada sobre este spot. Con la vieja resta
        // ("MaxBB == MinBB - 1") 1.5 == 2.5 - 1 igual, así que el stack de
        // en medio no alcanzaba a cortar la fusión: fusionar inventaría una
        // banda "0.5-3.5bb" que nadie declaró.
        var analizador = AnalizadorSintetico("SIT", "ALL-IN",
            ("0.5-1.5bb", 0.5m, 1.5m, ["SPOT"]),
            ("1.5-2.5bb", 1.5m, 2.5m, ["OTRO_SPOT"]),
            ("2.5-3.5bb", 2.5m, 3.5m, ["SPOT"]));

        var umbral = analizador.Analizar("SIT", "0.5-1.5bb", "SPOT", "A8o")!.Umbral;

        Assert.Equal(2, umbral.Count);
        Assert.Equal("0.5-1.5bb", umbral[0].ClaveDeStack);
        Assert.Equal("2.5-3.5bb", umbral[1].ClaveDeStack);
    }

    [Fact]
    public void Dos_stacks_decimales_contiguos_dan_una_sola_banda()
    {
        // 8.5-9.5 seguido de 9.5-10.5: contiguos de verdad, aunque "-1" en
        // decimal no lo detecte (9.5 != 9.5 - 1).
        var analizador = AnalizadorSintetico("SIT", "ALL-IN",
            ("8.5-9.5bb", 8.5m, 9.5m, ["SPOT"]),
            ("9.5-10.5bb", 9.5m, 10.5m, ["SPOT"]));

        var umbral = analizador.Analizar("SIT", "8.5-9.5bb", "SPOT", "A8o")!.Umbral;

        Assert.Single(umbral);
        Assert.Equal("8.5-9.5bb…9.5-10.5bb", umbral[0].ClaveDeStack);
    }
}
