using System;
using System.Linq;
using CashHub.Validation.Models;
using Xunit;

namespace CashHub.Validation.Tests
{
    /// <summary>
    /// El mapeo de sanic_recordstatus a vigencia es la pieza que arregla el bypass: antes
    /// el motor filtraba por statecode = 0, y CashHub nunca escribe statecode en las tablas
    /// maestras (la baja va por sanic_recordstatus). El filtro era un no-op y los registros
    /// dados de baja pasaban las cuatro reglas de autorizacion.
    ///
    /// Se prueba exhaustivo: los cuatro valores del option set, null, y valores fuera del
    /// dominio. Un default equivocado aca reabre el agujero entero.
    /// </summary>
    public class RecordVigencyMapTests
    {
        [Theory]
        [InlineData(RecordVigencyMap.Active)]
        [InlineData(RecordVigencyMap.PendingDeactivation)]
        public void Activo_y_pendiente_de_baja_operan(int recordStatus)
        {
            // PendingDeactivation opera hasta que el aprobador firme la baja.
            Assert.Equal(Vigency.Vigente, RecordVigencyMap.FromRecordStatus(recordStatus));
        }

        [Fact]
        public void Inactivo_esta_revocado()
        {
            Assert.Equal(Vigency.Revocado, RecordVigencyMap.FromRecordStatus(RecordVigencyMap.Inactive));
        }

        [Fact]
        public void Pendiente_de_aprobacion_no_autoriza()
        {
            // Es el AppDefaultValue de un registro recien creado: un alta sin firmar.
            // Se distingue de Inactive solo para el tracing; no habilita nada.
            var vigency = RecordVigencyMap.FromRecordStatus(RecordVigencyMap.PendingApproval);

            Assert.Equal(Vigency.Pendiente, vigency);
            Assert.False(RecordVigencyMap.IsVigent(vigency));
        }

        [Fact]
        public void Sin_valor_no_se_otorga_confianza()
        {
            // Columna vacia = sin evidencia de vigencia. Falla cerrado.
            Assert.Equal(Vigency.Revocado, RecordVigencyMap.FromRecordStatus(null));
        }

        [Theory]
        [InlineData(0)]
        [InlineData(-1)]
        [InlineData(159460009)]
        [InlineData(int.MaxValue)]
        public void Un_valor_fuera_del_option_set_no_autoriza(int recordStatus)
        {
            // Si negocio agrega un estado nuevo al option set y nadie toca este mapa, el
            // registro deja de operar. Es lo correcto: mejor rechazar de mas que autorizar
            // un estado que nadie penso.
            Assert.False(RecordVigencyMap.IsVigent(RecordVigencyMap.FromRecordStatus(recordStatus)));
        }

        /// <summary>
        /// Invariante que impide que el filtro de la consulta y la traduccion de la fila
        /// se desincronicen: si un valor esta en VigentValues tiene que traducir a Vigente,
        /// y si traduce a Vigente tiene que estar en VigentValues. Cuando divergen, la
        /// consulta trae filas que la traduccion rechaza — o al reves, que es el bypass.
        /// </summary>
        [Fact]
        public void El_filtro_de_consulta_y_la_traduccion_no_pueden_divergir()
        {
            var todos = new[]
            {
                RecordVigencyMap.Active,
                RecordVigencyMap.Inactive,
                RecordVigencyMap.PendingApproval,
                RecordVigencyMap.PendingDeactivation
            };

            foreach (var value in todos)
            {
                var esVigentePorTraduccion = RecordVigencyMap.IsVigent(RecordVigencyMap.FromRecordStatus(value));
                var esVigentePorFiltro = RecordVigencyMap.VigentValues.Contains(value);

                Assert.True(esVigentePorTraduccion == esVigentePorFiltro,
                    $"El valor {value} es vigente segun {(esVigentePorTraduccion ? "la traduccion" : "el filtro")} " +
                    $"pero no segun {(esVigentePorTraduccion ? "el filtro" : "la traduccion")}.");
            }
        }

        [Fact]
        public void El_filtro_no_puede_quedar_vacio()
        {
            // Un VigentValues vacio haria que toda consulta filtrada devuelva cero filas y
            // el sistema rechace absolutamente todo sin explicacion.
            Assert.NotEmpty(RecordVigencyMap.VigentValues);
        }
    }
}
