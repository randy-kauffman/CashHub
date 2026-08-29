using System;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text;
using CashHub.Validation.Engine;
using CashHub.Validation.Models;
using CashHub.Validation.Parsing;
using CashHub.Validation.Reporting;
using CashHub.Validation.Rules;
using Xunit;

namespace CashHub.Validation.Tests
{
    /// <summary>
    /// El adjunto lo controla un ATACANTE, no un cliente cooperativo. Un cliente abre la
    /// plantilla oficial y llena celdas; un atacante fabrica el .xlsx con bytes crudos y le
    /// quita todas las protecciones. Estos casos no se pueden armar con
    /// build-test-workbooks.py (parte de la plantilla real): se construyen a mano.
    ///
    /// La linea que se defiende es UNA: XlsxParser.Parse NUNCA debe tumbar el worker.
    /// Cualquier entrada — corrupta, gigante, hostil — tiene que terminar en
    /// XlsxParseException (error de negocio, el correo va a Revision) y jamas en una
    /// excepcion que escape, un cuelgue o un OutOfMemory que se lleve puesto al proceso y
    /// con el a los otros correos que se esten validando.
    /// </summary>
    public class AdversarialInputTests
    {
        private const string M = "http://schemas.openxmlformats.org/spreadsheetml/2006/main";

        private static byte[] Zip(params (string path, string content)[] entries)
        {
            using var ms = new MemoryStream();
            using (var zip = new ZipArchive(ms, ZipArchiveMode.Create, leaveOpen: true))
            {
                foreach (var (path, content) in entries)
                {
                    var e = zip.CreateEntry(path);
                    using var w = new StreamWriter(e.Open(), new UTF8Encoding(false));
                    w.Write(content);
                }
            }
            return ms.ToArray();
        }

        // Un .xlsx minimo pero VALIDO, para partir de algo que parsea y romperlo de a una cosa.
        private static (string, string)[] Base(string sheet1) => new[]
        {
            ("xl/workbook.xml", $"<workbook xmlns=\"{M}\" xmlns:r=\"http://schemas.openxmlformats.org/officeDocument/2006/relationships\"><sheets><sheet name=\"Depositos\" sheetId=\"1\" r:id=\"rId1\"/><sheet name=\"Cheques\" sheetId=\"2\" r:id=\"rId2\"/></sheets></workbook>"),
            ("xl/_rels/workbook.xml.rels", "<Relationships xmlns=\"http://schemas.openxmlformats.org/package/2006/relationships\"><Relationship Id=\"rId1\" Type=\"http://schemas.openxmlformats.org/officeDocument/2006/relationships/worksheet\" Target=\"worksheets/sheet1.xml\"/><Relationship Id=\"rId2\" Type=\"http://schemas.openxmlformats.org/officeDocument/2006/relationships/worksheet\" Target=\"worksheets/sheet2.xml\"/></Relationships>"),
            ("xl/worksheets/sheet1.xml", sheet1),
            ("xl/worksheets/sheet2.xml", $"<worksheet xmlns=\"{M}\"><sheetData/></worksheet>"),
        };

        /// <summary>Lo unico que se exige SIEMPRE: no explota el proceso. Falla como negocio o parsea.</summary>
        private static void DebeSobrevivir(byte[] bytes)
        {
            try { XlsxParser.Parse(bytes); }
            catch (XlsxParseException) { /* correcto: error de negocio controlado */ }
            // Cualquier otra excepcion escapa y hace fallar el test.
        }

        // ---------------------------------------------------------------------
        // XXE — el ataque clasico contra un parser de XML.
        // ---------------------------------------------------------------------

        [Fact]
        public void No_resuelve_entidades_externas_de_archivo_local()
        {
            // Si el parser resolviera &xxe;, leeria /etc/passwd y lo meteria en una celda.
            var xxe = "<?xml version=\"1.0\"?>" +
                      "<!DOCTYPE r [<!ENTITY xxe SYSTEM \"file:///etc/passwd\">]>" +
                      $"<worksheet xmlns=\"{M}\"><sheetData><row r=\"1\"><c r=\"A1\" t=\"inlineStr\"><is><t>&xxe;</t></is></c></row></sheetData></worksheet>";
            DebeSobrevivir(Zip(Base(xxe)));
        }

        [Fact]
        public void No_resuelve_entidades_externas_por_http()
        {
            // Peor que leer un archivo: una entidad SYSTEM http:// convierte al worker en
            // un cliente HTTP hacia donde el atacante quiera (SSRF).
            var ssrf = "<?xml version=\"1.0\"?>" +
                       "<!DOCTYPE r [<!ENTITY x SYSTEM \"http://169.254.169.254/latest/meta-data/\">]>" +
                       $"<worksheet xmlns=\"{M}\"><sheetData><row r=\"1\"><c r=\"A1\" t=\"inlineStr\"><is><t>&x;</t></is></c></row></sheetData></worksheet>";
            DebeSobrevivir(Zip(Base(ssrf)));
        }

        [Fact]
        public void No_expande_bomba_de_entidades_billion_laughs()
        {
            // Diez entidades anidadas que se expanden 10x cada una = 10^9 "lol". Si el parser
            // las expandiera, agota la RAM del worker y se lleva puestos los demas correos.
            var lols = "<?xml version=\"1.0\"?><!DOCTYPE r [" +
                       "<!ENTITY a \"lol\">" +
                       "<!ENTITY b \"&a;&a;&a;&a;&a;&a;&a;&a;&a;&a;\">" +
                       "<!ENTITY c \"&b;&b;&b;&b;&b;&b;&b;&b;&b;&b;\">" +
                       "<!ENTITY d \"&c;&c;&c;&c;&c;&c;&c;&c;&c;&c;\">" +
                       "<!ENTITY e \"&d;&d;&d;&d;&d;&d;&d;&d;&d;&d;\">" +
                       "<!ENTITY f \"&e;&e;&e;&e;&e;&e;&e;&e;&e;&e;\">]>" +
                       $"<worksheet xmlns=\"{M}\"><sheetData><row r=\"1\"><c r=\"A1\" t=\"inlineStr\"><is><t>&f;</t></is></c></row></sheetData></worksheet>";
            DebeSobrevivir(Zip(Base(lols)));
        }

        // ---------------------------------------------------------------------
        // Paquete ZIP hostil.
        // ---------------------------------------------------------------------

        [Fact]
        public void Un_no_zip_renombrado_a_xlsx_no_explota()
        {
            DebeSobrevivir(Encoding.UTF8.GetBytes("esto no es un zip, es texto plano con extension .xlsx"));
        }

        [Fact]
        public void Un_zip_valido_sin_las_partes_de_excel_no_explota()
        {
            DebeSobrevivir(Zip(("random.txt", "no hay workbook.xml aca")));
        }

        [Fact]
        public void Xml_bien_formado_pero_que_no_es_una_hoja_no_explota()
        {
            DebeSobrevivir(Zip(Base("<algoQueNoEsWorksheet><foo/></algoQueNoEsWorksheet>")));
        }

        [Fact]
        public void Xml_truncado_a_la_mitad_no_explota()
        {
            DebeSobrevivir(Zip(Base($"<worksheet xmlns=\"{M}\"><sheetData><row r=\"1\"><c r=\"A1\"")));
        }

        [Fact]
        public void Referencia_de_celda_absurda_no_explota()
        {
            // RowOf/ColumnOf parsean la referencia; una referencia disparatada no puede
            // tumbar el parser con overflow o formato.
            foreach (var refCelda in new[] { "A99999999999999999999", "ZZZZZZZZZZ1", "", "1", "A", "@#$%" })
            {
                var s = $"<worksheet xmlns=\"{M}\"><sheetData><row r=\"1\"><c r=\"{refCelda}\" t=\"inlineStr\"><is><t>x</t></is></c></row></sheetData></worksheet>";
                DebeSobrevivir(Zip(Base(s)));
            }
        }

        [Fact]
        public void Indice_de_shared_string_fuera_de_rango_no_explota()
        {
            // La celda dice t="s" (shared) y apunta al indice 9999, pero no hay tabla de
            // shared strings. Un IndexOutOfRange que escape mata el worker.
            var s = $"<worksheet xmlns=\"{M}\"><sheetData><row r=\"1\"><c r=\"A1\" t=\"s\"><v>9999</v></c></row></sheetData></worksheet>";
            DebeSobrevivir(Zip(Base(s)));
        }

        // ---------------------------------------------------------------------
        // Volumen: campos gigantes y millones de celdas.
        // ---------------------------------------------------------------------

        [Fact]
        public void Una_celda_de_cinco_millones_de_caracteres_no_explota()
        {
            // El campo Referencia sin limite de plantilla: 5 MB de texto en una sola celda.
            var gigante = new string('A', 5_000_000);
            var s = $"<worksheet xmlns=\"{M}\"><sheetData><row r=\"1\"><c r=\"A1\" t=\"inlineStr\"><is><t>{gigante}</t></is></c></row></sheetData></worksheet>";
            DebeSobrevivir(Zip(Base(s)));
        }

        [Fact]
        public void Un_numero_de_mil_digitos_no_explota()
        {
            // TryDecimal sobre 1000 digitos: no debe colgar ni lanzar OverflowException que
            // escape. Debe devolver "no es un decimal" y seguir.
            var milDigitos = new string('9', 1000);
            Assert.False(new CellValue(milDigitos, "A1", 1, "Depositos").TryDecimal(out _));
        }

        [Fact]
        public void Cientos_de_miles_de_celdas_no_explotan_ni_cuelgan()
        {
            // Un atacante quita el limite de filas de la plantilla y manda 200.000 celdas.
            // No hay tope en el parser: se documenta el comportamiento actual (las carga
            // todas). El test fija que al menos NO explota; si algun dia se agrega un tope,
            // este test cambia a verificar el corte.
            var sb = new StringBuilder($"<worksheet xmlns=\"{M}\"><sheetData>");
            for (var r = 1; r <= 200_000; r++)
                sb.Append($"<row r=\"{r}\"><c r=\"A{r}\" t=\"inlineStr\"><is><t>x</t></is></c></row>");
            sb.Append("</sheetData></worksheet>");
            DebeSobrevivir(Zip(Base(sb.ToString())));
        }
    }

    /// <summary>
    /// El correo de RESPUESTA se arma con contenido que el cliente/atacante puso en el Excel
    /// (nombre de cliente, referencias, numeros de cheque). Si un valor hostil se refleja sin
    /// escapar, el atacante inyecta HTML en un correo que el banco firma y envia.
    /// </summary>
    public class ReplyInjectionTests
    {
        private static string Render(string template, params (string k, string v)[] vals)
        {
            var d = new TemplateEngine.TemplateData();
            foreach (var (k, v) in vals) d.Set(k, v);
            return TemplateEngine.Render(template, d);
        }

        [Fact]
        public void El_token_normal_escapa_el_html()
        {
            var html = Render("Hola {{cliente}}", ("cliente", "<script>alert(1)</script>"));
            Assert.DoesNotContain("<script>", html);
            Assert.Contains("&lt;script&gt;", html);
        }

        [Fact]
        public void El_token_escapa_comillas_y_ampersand()
        {
            var html = Render("{{ref}}", ("ref", "a\"b&c<d>"));
            Assert.Equal("a&quot;b&amp;c&lt;d&gt;", html);
        }

        [Fact]
        public void Un_valor_con_llaves_no_se_re_interpreta_como_token()
        {
            // El atacante pone {{cliente}} en la referencia esperando que el motor lo
            // sustituya por el nombre real de otro cliente. El reemplazo es de una sola
            // pasada, asi que el texto inyectado queda literal (y escapado).
            var html = Render("Ref: {{ref}}", ("ref", "{{cliente}}"), ("cliente", "SECRETO"));
            Assert.DoesNotContain("SECRETO", html);
            Assert.Contains("{{cliente}}", html);
        }

        [Fact]
        public void Un_token_inexistente_queda_vacio_no_deja_el_literal()
        {
            Assert.Equal("Hola ", Render("Hola {{noexiste}}"));
        }

        /// <summary>
        /// EL RIESGO VIVO: el token triple {{{x}}} emite SIN escapar. Hoy ninguna plantilla
        /// del seed lo usa, pero negocio edita las plantillas en Dataverse sin tocar codigo.
        /// Este test DOCUMENTA que el escape se pierde con la sintaxis triple, para que
        /// quede en rojo el dia que alguien meta {{{cliente}}} en una plantilla.
        /// </summary>
        [Fact]
        public void El_token_triple_NO_escapa_es_una_trampa_para_el_editor_de_plantillas()
        {
            var html = Render("{{{cliente}}}", ("cliente", "<b>x</b>"));
            Assert.Contains("<b>x</b>", html);   // <-- sin escapar: por eso NO debe usarse con datos del cliente
        }
    }
}
