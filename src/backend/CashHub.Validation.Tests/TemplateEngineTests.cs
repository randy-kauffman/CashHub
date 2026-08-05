using System.Collections.Generic;
using CashHub.Validation.Reporting;
using Xunit;

namespace CashHub.Validation.Tests
{
    public class TemplateEngineTests
    {
        private static TemplateEngine.TemplateData Data() => new();

        [Fact]
        public void Reemplaza_tokens_simples_escapando_el_contenido()
        {
            var html = TemplateEngine.Render("Hola {{cliente}}",
                Data().Set("cliente", "Empresa <b>X</b>"));
            Assert.Equal("Hola Empresa &lt;b&gt;X&lt;/b&gt;", html);
        }

        [Fact]
        public void Token_triple_no_escapa_para_html_ya_armado()
        {
            var html = TemplateEngine.Render("{{{bloque}}}",
                Data().Set("bloque", "<tr><td>ok</td></tr>"));
            Assert.Equal("<tr><td>ok</td></tr>", html);
        }

        [Fact]
        public void Token_desconocido_se_borra_en_vez_de_mostrar_las_llaves()
        {
            var html = TemplateEngine.Render("A{{noexiste}}B", Data());
            Assert.Equal("AB", html);
        }

        [Fact]
        public void Repite_el_bloque_por_cada_fila_de_la_lista()
        {
            var data = Data().SetList("errores", new[]
            {
                new Dictionary<string, string?> { ["celda"] = "O7", ["texto"] = "negativo" },
                new Dictionary<string, string?> { ["celda"] = "J8", ["texto"] = "sin bolsa" }
            });
            var html = TemplateEngine.Render("<ul>{{#errores}}<li>{{celda}}: {{texto}}</li>{{/errores}}</ul>", data);
            Assert.Equal("<ul><li>O7: negativo</li><li>J8: sin bolsa</li></ul>", html);
        }

        [Fact]
        public void Dentro_del_bloque_tambien_ve_los_tokens_globales()
        {
            var data = Data().Set("hoja", "Depositos")
                .SetList("filas", new[] { new Dictionary<string, string?> { ["celda"] = "O7" } });
            var html = TemplateEngine.Render("{{#filas}}{{hoja}}!{{celda}}{{/filas}}", data);
            Assert.Equal("Depositos!O7", html);
        }

        [Fact]
        public void Lista_vacia_borra_el_bloque_completo()
        {
            var data = Data().SetList("errores", new List<Dictionary<string, string?>>());
            var html = TemplateEngine.Render("inicio{{#errores}}<li>{{x}}</li>{{/errores}}fin", data);
            Assert.Equal("iniciofin", html);
        }

        [Fact]
        public void Bloque_condicional_muestra_solo_cuando_hay_valor()
        {
            const string tpl = "{{#si:gestion}}Gestion: {{gestion}}{{/si}}";
            Assert.Equal("Gestion: Email-42", TemplateEngine.Render(tpl, Data().Set("gestion", "Email-42")));
            Assert.Equal("", TemplateEngine.Render(tpl, Data().Set("gestion", "")));
            Assert.Equal("", TemplateEngine.Render(tpl, Data()));
        }

        [Fact]
        public void Bloque_condicional_tambien_funciona_con_listas()
        {
            const string tpl = "{{#si:errores}}hay errores{{/si}}";
            Assert.Equal("hay errores", TemplateEngine.Render(tpl,
                Data().SetList("errores", new[] { new Dictionary<string, string?>() })));
            Assert.Equal("", TemplateEngine.Render(tpl,
                Data().SetList("errores", new List<Dictionary<string, string?>>())));
        }

        [Fact]
        public void Una_plantilla_realista_se_arma_completa()
        {
            var data = Data()
                .Set("titulo", "Encontramos 2 puntos por corregir")
                .Set("gestion", "Email-17")
                .SetList("errores", new[]
                {
                    new Dictionary<string, string?> { ["hoja"] = "Depositos", ["celda"] = "O7", ["mensaje"] = "negativo" },
                    new Dictionary<string, string?> { ["hoja"] = "Cheques", ["celda"] = "A3", ["mensaje"] = "huerfano" }
                });
            const string tpl = @"<h1>{{titulo}}</h1><table>{{#errores}}<tr><td>{{hoja}}!{{celda}}</td><td>{{mensaje}}</td></tr>{{/errores}}</table>{{#si:gestion}}<p>Gestion {{gestion}}</p>{{/si}}";
            var html = TemplateEngine.Render(tpl, data);

            Assert.Contains("<h1>Encontramos 2 puntos por corregir</h1>", html);
            Assert.Contains("<td>Depositos!O7</td><td>negativo</td>", html);
            Assert.Contains("<td>Cheques!A3</td><td>huerfano</td>", html);
            Assert.Contains("<p>Gestion Email-17</p>", html);
        }
    }
}
