using System;
using System.Collections.Generic;
using CashHub.Validation.Reporting;
using Xunit;

namespace CashHub.Validation.Tests
{
    public class ClientReplyBuilderTests
    {
        private static readonly DateTime Recibido = new(2026, 8, 5, 15, 7, 0, DateTimeKind.Utc);

        [Fact]
        public void Aceptado_muestra_totales_por_moneda_y_pasos_siguientes()
        {
            var html = ClientReplyBuilder.BuildAccepted("Distribuidora El Porvenir, S.A.", "declaracion.xlsx",
                new[] { new ReplyBagSummary("123456789", 4, 1000m, 600m, 0m) }, Recibido);

            Assert.Contains("Declaracion recibida y validada", html);
            Assert.Contains("123456789", html);
            Assert.Contains("1,000.00", html);
            Assert.Contains("600.00", html);
            Assert.Contains("transportadora de valores", html);
            Assert.Contains("Distribuidora El Porvenir", html);
            Assert.Contains("05/08/2026 15:07", html);
        }

        [Fact]
        public void Aceptado_con_varias_bolsas_agrega_fila_de_total()
        {
            var html = ClientReplyBuilder.BuildAccepted("Cliente", "d.xlsx", new[]
            {
                new ReplyBagSummary("B-1", 2, 500m, 0m, 0m),
                new ReplyBagSummary("B-2", 3, 250m, 100m, 0m)
            }, Recibido);

            Assert.Contains(">Total<", html);
            Assert.Contains("750.00", html);   // suma NIO
            Assert.Contains(">5<", html);       // suma de depósitos
        }

        [Fact]
        public void Rechazado_separa_errores_de_sobre_de_los_del_archivo()
        {
            var html = ClientReplyBuilder.BuildRejected("Cliente", "d.xlsx", new[]
            {
                new ReplyIssue("El asunto del correo debe comenzar con 'Pre-registro'."),
                new ReplyIssue("La celda O7 de la hoja Depositos tiene un valor negativo (-500).",
                    sheet: "Depositos", cellRef: "O7", row: 7)
            }, Recibido);

            Assert.Contains("Encontramos 2 puntos por corregir", html);
            Assert.Contains("Sobre el correo y el archivo", html);
            Assert.Contains("En el archivo de Excel", html);
            Assert.Contains("Depositos!O7", html);
            Assert.Contains("NO fue registrada", html);
        }

        [Fact]
        public void Rechazado_con_un_solo_error_usa_singular()
        {
            var html = ClientReplyBuilder.BuildRejected(null, "d.xlsx",
                new[] { new ReplyIssue("El CIF esta vacio.") }, Recibido);

            Assert.Contains("Encontramos 1 punto por corregir", html);
            Assert.DoesNotContain("puntos por corregir", html);
        }

        [Fact]
        public void Rechazado_ordena_los_errores_del_archivo_por_fila()
        {
            var html = ClientReplyBuilder.BuildRejected("Cliente", "d.xlsx", new[]
            {
                new ReplyIssue("error fila 12", sheet: "Depositos", cellRef: "J12", row: 12),
                new ReplyIssue("error fila 6", sheet: "Depositos", cellRef: "J6", row: 6)
            }, Recibido);

            Assert.True(html.IndexOf("J6", StringComparison.Ordinal) < html.IndexOf("J12", StringComparison.Ordinal),
                "los errores deben listarse en el orden en que el cliente los va a recorrer");
        }

        [Fact]
        public void El_contenido_del_cliente_se_escapa_para_no_romper_el_html()
        {
            var html = ClientReplyBuilder.BuildRejected("Empresa <script>alert(1)</script> S.A.", "a&b.xlsx",
                new[] { new ReplyIssue("valor '<b>raro</b>'") }, Recibido);

            Assert.DoesNotContain("<script>", html);
            Assert.Contains("&lt;script&gt;", html);
            Assert.Contains("a&amp;b.xlsx", html);
        }

        [Theory]
        [InlineData("style=")]      // estilos inline: los clientes de correo descartan <style>
        [InlineData("role='presentation'")] // tablas de layout accesibles
        [InlineData("max-width:640px")]     // ancho legible en escritorio
        public void Cumple_las_reglas_de_html_para_correo(string requisito)
        {
            var html = ClientReplyBuilder.BuildAccepted("C", "d.xlsx",
                new[] { new ReplyBagSummary("B", 1, 1m, 0m, 0m) }, Recibido);
            Assert.Contains(requisito, html);
        }

        [Fact]
        public void No_referencia_imagenes_externas_ni_hojas_de_estilo()
        {
            var html = ClientReplyBuilder.BuildRejected("C", "d.xlsx",
                new[] { new ReplyIssue("x") }, Recibido);
            Assert.DoesNotContain("<img", html);
            Assert.DoesNotContain("<style", html);
            Assert.DoesNotContain("http://", html);
        }
    }
}
