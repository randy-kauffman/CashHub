using System;
using System.Collections.Generic;
using System.Linq;
using CashHub.Validation.Security;
using Xunit;

namespace CashHub.Validation.Tests
{
    /// <summary>
    /// Los asientos que genera el cierre de un recuento. Salian de scrAprobaciones
    /// (CH-App-Supervisor) como Power Fx sin un solo test, generando plata contra
    /// cuentas de clientes.
    ///
    /// La regla que mas importa y la que mas facil se rompe: LA DIFERENCIA SE CALCULA
    /// POR COMPONENTE, NUNCA POR EL NETO. Un deposito con 100 de mas en efectivo y 100
    /// de menos en cheques NO es un cierre limpio: es un sobrante de 100 Y un faltante
    /// de 100, y los dos tienen que quedar asentados.
    /// </summary>
    public class RecountClosureAccountingTests
    {
        private const int NIO = 159460000;
        private const int USD = 159460001;

        private static readonly Guid R1 = Guid.Parse("11111111-1111-1111-1111-111111111111");
        private static readonly Guid R2 = Guid.Parse("22222222-2222-2222-2222-222222222222");
        private static readonly Guid D1 = Guid.Parse("aaaaaaaa-1111-1111-1111-111111111111");
        private static readonly Guid D2 = Guid.Parse("bbbbbbbb-2222-2222-2222-222222222222");

        private static RecountedDeposit Rec(
            Guid id, decimal cash = 0, decimal bac = 0, decimal otro = 0,
            int cuenta = 1001, int currency = NIO, int? txNo = 77) =>
            new RecountedDeposit(id, cuenta, currency, cash, bac, otro, txNo);

        private static DeclaredDeposit Dec(
            Guid id, Guid? relacionado, decimal cash = 0, decimal bac = 0, decimal otro = 0,
            int cuenta = 2002, int currency = NIO, int? txNo = 88,
            string? referencia = "REF-DEP") =>
            new DeclaredDeposit(id, cuenta, currency, cash, bac, otro, txNo, referencia, relacionado);

        // =====================================================================
        // 1. Recontado SIN declarado: sobrante puro
        // =====================================================================

        [Fact]
        public void RecontadoSinDeclarado_GeneraUnSobranteConLaCuentaDelRecuento()
        {
            var asientos = RecountClosureAccounting.ForBagWithDifferences(
                "T-001",
                new[] { Rec(R1, cash: 500, cuenta: 1001) },
                Array.Empty<DeclaredDeposit>());

            var a = Assert.Single(asientos);
            Assert.Equal(LedgerEntryType.Sobrante, a.Type);
            Assert.Equal(1001, a.AccountNumber);
            Assert.Equal(500m, a.CashAmount);
            Assert.Equal("SS/T-001", a.Reference);
            Assert.Equal(LedgerProcessing.Manual, a.Processing);
            Assert.Equal(R1, a.RelatedRecountedDepositId);
        }

        // =====================================================================
        // 2. Enlazados: diferencia POR COMPONENTE
        // =====================================================================

        [Fact]
        public void Enlazado_Exacto_SoloGeneraElDeposito()
        {
            var asientos = RecountClosureAccounting.ForBagWithDifferences(
                "T-002",
                new[] { Rec(R1, cash: 1000) },
                new[] { Dec(D1, R1, cash: 1000) });

            var a = Assert.Single(asientos);
            Assert.Equal(LedgerEntryType.Deposito, a.Type);
            Assert.Equal(1000m, a.CashAmount);
            Assert.Equal("REF-DEP", a.Reference);
        }

        [Fact]
        public void Enlazado_SobranteYFaltanteALaVez_NoSeNetean()
        {
            // EL test de esta clase. +100 en efectivo y -100 en cheques BAC.
            // Por el neto daria cero y no se asentaria nada: plata que se pierde.
            var asientos = RecountClosureAccounting.ForBagWithDifferences(
                "T-003",
                new[] { Rec(R1, cash: 1100, bac: 0) },
                new[] { Dec(D1, R1, cash: 1000, bac: 100) });

            var sobrante = Assert.Single(asientos.Where(x => x.Type == LedgerEntryType.Sobrante));
            Assert.Equal(100m, sobrante.CashAmount);
            Assert.Equal(0m, sobrante.BacCheckAmount);
            Assert.Equal("S/T-003", sobrante.Reference);

            var faltante = Assert.Single(asientos.Where(x => x.Type == LedgerEntryType.Faltante));
            Assert.Equal(0m, faltante.CashAmount);
            Assert.Equal(100m, faltante.BacCheckAmount);
            Assert.Equal("F/T-003", faltante.Reference);

            Assert.Single(asientos.Where(x => x.Type == LedgerEntryType.Deposito));
        }

        [Fact]
        public void Enlazado_LasDiferenciasUsanLaCuentaDeclarada()
        {
            // La cuenta del asiento de diferencia sale del DECLARADO, no del recuento:
            // la plata se le acredita o debita a la cuenta que el cliente declaro.
            var asientos = RecountClosureAccounting.ForBagWithDifferences(
                "T-004",
                new[] { Rec(R1, cash: 900, cuenta: 1001) },
                new[] { Dec(D1, R1, cash: 1000, cuenta: 2002) });

            foreach (var a in asientos)
            {
                Assert.Equal(2002, a.AccountNumber);
            }
        }

        [Fact]
        public void Enlazado_ConCheques_ElDepositoEsManual_SinCheques_EsBatch()
        {
            var conCheques = RecountClosureAccounting.ForBagWithDifferences(
                "T-005", new[] { Rec(R1, cash: 10, bac: 5) }, new[] { Dec(D1, R1, cash: 10, bac: 5) });
            Assert.Equal(LedgerProcessing.Manual,
                conCheques.Single(x => x.Type == LedgerEntryType.Deposito).Processing);

            var sinCheques = RecountClosureAccounting.ForBagWithDifferences(
                "T-005", new[] { Rec(R1, cash: 10) }, new[] { Dec(D1, R1, cash: 10) });
            Assert.Equal(LedgerProcessing.Batch,
                sinCheques.Single(x => x.Type == LedgerEntryType.Deposito).Processing);
        }

        [Fact]
        public void Enlazado_ElDepositoLlevaLosMontosRECONTADOS()
        {
            // El asiento de deposito refleja lo que REALMENTE se conto, no lo declarado.
            var asientos = RecountClosureAccounting.ForBagWithDifferences(
                "T-006", new[] { Rec(R1, cash: 900) }, new[] { Dec(D1, R1, cash: 1000) });

            Assert.Equal(900m, asientos.Single(x => x.Type == LedgerEntryType.Deposito).CashAmount);
        }

        // =====================================================================
        // 3. Declarado que nadie reconto: faltante + su contrapartida
        // =====================================================================

        [Fact]
        public void DeclaradoSinRecontar_GeneraFaltanteYDeposito_AmbosManuales()
        {
            var asientos = RecountClosureAccounting.ForBagWithDifferences(
                "T-007",
                Array.Empty<RecountedDeposit>(),
                new[] { Dec(D1, relacionado: null, cash: 750, currency: USD) });

            Assert.Equal(2, asientos.Count);

            var faltante = asientos.Single(x => x.Type == LedgerEntryType.Faltante);
            Assert.Equal(750m, faltante.CashAmount);
            Assert.Equal("F/T-007", faltante.Reference);
            Assert.Equal(USD, faltante.Currency);

            var deposito = asientos.Single(x => x.Type == LedgerEntryType.Deposito);
            Assert.Equal("REF-DEP", deposito.Reference);

            // Sin recuento al que apuntar: el vinculo queda vacio, nunca inventado.
            Assert.All(asientos, a => Assert.Null(a.RelatedRecountedDepositId));
            Assert.All(asientos, a => Assert.Equal(LedgerProcessing.Manual, a.Processing));
        }

        // =====================================================================
        // 4. Escenario mixto completo
        // =====================================================================

        [Fact]
        public void EscenarioMixto_CubreLasTresRamas()
        {
            var asientos = RecountClosureAccounting.ForBagWithDifferences(
                "T-008",
                new[] { Rec(R1, cash: 1000), Rec(R2, cash: 300) },   // R1 enlazado, R2 huerfano
                new[]
                {
                    Dec(D1, R1, cash: 1000),                          // calza exacto
                    Dec(D2, relacionado: null, cash: 400),            // declarado sin recontar
                });

            Assert.Equal(LedgerEntryType.Deposito, asientos[0].Type);        // R1 calza
            Assert.Contains(asientos, a => a.Type == LedgerEntryType.Sobrante
                                           && a.CashAmount == 300m);          // R2 huerfano
            Assert.Contains(asientos, a => a.Type == LedgerEntryType.Faltante
                                           && a.CashAmount == 400m);          // D2 sin recontar
            Assert.Equal(4, asientos.Count);
        }

        [Fact]
        public void SinDepositos_NoGeneraAsientos()
        {
            Assert.Empty(RecountClosureAccounting.ForBagWithDifferences(
                "T-009", Array.Empty<RecountedDeposit>(), Array.Empty<DeclaredDeposit>()));
        }

        // =====================================================================
        // 5. Recuento sin declaracion: un asiento por deposito recontado
        // =====================================================================

        [Fact]
        public void SinDeclaracion_UnAsientoPorDepositoRecontado()
        {
            var asientos = RecountClosureAccounting.ForRecountWithoutDeclaration(
                "T-010",
                new[] { Rec(R1, cash: 100, cuenta: 3003), Rec(R2, bac: 250, cuenta: 4004) });

            Assert.Equal(2, asientos.Count);
            Assert.All(asientos, a =>
            {
                Assert.Equal(LedgerEntryType.SinDeclaracion, a.Type);
                Assert.Equal(LedgerProcessing.Manual, a.Processing);
                Assert.Equal("SD/T-010", a.Reference);
            });

            Assert.Equal(3003, asientos[0].AccountNumber);
            Assert.Equal(100m, asientos[0].CashAmount);
            Assert.Equal(R1, asientos[0].RelatedRecountedDepositId);
            Assert.Equal(250m, asientos[1].BacCheckAmount);
        }

        [Fact]
        public void SinDeclaracion_SinDepositos_NoGeneraAsientos()
        {
            Assert.Empty(RecountClosureAccounting.ForRecountWithoutDeclaration(
                "T-011", Array.Empty<RecountedDeposit>()));
        }

        // =====================================================================
        // 6. Entradas degeneradas — el productor es una app, no un contrato firme
        // =====================================================================

        [Fact]
        public void ColeccionesNulas_NoExplotan()
        {
            Assert.Empty(RecountClosureAccounting.ForBagWithDifferences("T-012", null, null));
            Assert.Empty(RecountClosureAccounting.ForRecountWithoutDeclaration("T-012", null));
        }

        [Fact]
        public void DeclaradoQueApuntaAUnRecuentoInexistente_SeTrataComoNoRecontado()
        {
            // Si el enlace apunta a un recuento que no vino en la tula, el declarado
            // NO puede darse por recontado: seria dar por buena plata que nadie conto.
            var asientos = RecountClosureAccounting.ForBagWithDifferences(
                "T-013",
                Array.Empty<RecountedDeposit>(),
                new[] { Dec(D1, relacionado: R2, cash: 500) });

            Assert.Contains(asientos, a => a.Type == LedgerEntryType.Faltante && a.CashAmount == 500m);
            Assert.Equal(2, asientos.Count);
        }
        // =====================================================================
        // 7. El tope de 30 caracteres que impone el CORE
        //
        //    NO es el tope de la columna (31): es el del sistema que consume el
        //    archivo. La referencia se descarga y se sube al core, y el core no
        //    acepta mas de 30.
        //
        //    Los prefijos son CODIGOS CORTOS acordados con quien lee el archivo
        //    del lado del core, para dejarle a la tula casi todo el presupuesto:
        //      S/  sobrante        F/  faltante
        //      SS/ sobrante sin declarar    SD/ sin declaracion
        //
        //    NO se trunca a proposito. El numero de tula es lo que identifica el
        //    asiento del otro lado; recortarlo manda al core una referencia
        //    equivocada — y dos tulas distintas podrian colapsar en la misma. Un
        //    cierre bloqueado se reintenta; un asiento mal referenciado ya viajo.
        //    Por eso se construye entera y el llamador decide (la CAPI la rechaza
        //    como dato antes de escribir nada).
        // =====================================================================

        [Fact]
        public void ElTopeEsElDelCore_TreintaNoTreintaYUno()
        {
            Assert.Equal(30, RecountClosureAccounting.CoreReferenceMaxLength);
        }

        [Fact]
        public void TulasDeHoy_EntranComodas()
        {
            // La tula mas larga del entorno tiene 11 caracteres; 2971 de 3000 tienen 8.
            var asientos = RecountClosureAccounting.ForBagWithDifferences(
                "ST010561",
                new[] { Rec(R1, cash: 10), Rec(R2, cash: 5) },
                new[] { Dec(D1, R1, cash: 99) });

            Assert.All(asientos, a => Assert.False(
                RecountClosureAccounting.ExceedsCoreLimit(a.Reference),
                $"'{a.Reference}' mide {a.Reference.Length}"));

            Assert.Contains(asientos, a => a.Reference == "SS/ST010561");
            Assert.Contains(asientos, a => a.Reference == "F/ST010561");
        }

        [Theory]
        [InlineData(11)]   // la tula mas larga del entorno hoy
        [InlineData(27)]   // el techo que permite el prefijo mas largo ("SD/", "SS/")
        public void NingunPrefijo_SePasaConLasTulasQueExistenHoy(int largo)
        {
            var tula = new string('T', largo);
            var conDif = RecountClosureAccounting.ForBagWithDifferences(
                tula,
                new[] { Rec(R1, cash: 10), Rec(R2, cash: 5) },          // R2 huerfano -> "Sobrante s/decl "
                new[] { Dec(D1, R1, cash: 99), Dec(D2, null, cash: 7) });
            var sinDec = RecountClosureAccounting.ForRecountWithoutDeclaration(
                tula, new[] { Rec(R1, cash: 10) });

            foreach (var a in conDif.Concat(sinDec))
            {
                Assert.False(
                    RecountClosureAccounting.ExceedsCoreLimit(a.Reference),
                    $"'{a.Reference}' mide {a.Reference.Length}, tope {RecountClosureAccounting.CoreReferenceMaxLength}");
            }
        }

        [Fact]
        public void TulaLarga_NoSeTrunca_SeMarcaComoExcedida()
        {
            // 30 caracteres de tula + cualquier prefijo se pasa. La referencia se
            // devuelve ENTERA para que el llamador la rechace con un mensaje claro,
            // no mutilada.
            var tula = new string('X', 30);
            var asientos = RecountClosureAccounting.ForRecountWithoutDeclaration(
                tula, new[] { Rec(R1, cash: 10) });

            var r = asientos[0].Reference;
            Assert.Equal("SD/" + tula, r);                       // entera, sin recortar
            Assert.True(RecountClosureAccounting.ExceedsCoreLimit(r));
        }

        [Fact]
        public void LaReferenciaDelExcel_NoSeToca()
        {
            // El deposito conserva la referencia que mando el cliente. Solo se
            // sobrescribe en sobrante, faltante y sin declaracion.
            var asientos = RecountClosureAccounting.ForBagWithDifferences(
                "T-014",
                new[] { Rec(R1, cash: 10) },
                new[] { Dec(D1, R1, cash: 10, referencia: "SRC-84316010") });

            var dep = asientos.Single(a => a.Type == LedgerEntryType.Deposito);
            Assert.Equal("SRC-84316010", dep.Reference);
        }
    }
}
