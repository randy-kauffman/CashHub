using System;
using CashHub.Validation.Security;
using Xunit;
using static CashHub.Validation.Security.FieldValueConverter;

namespace CashHub.Validation.Tests
{
    /// <summary>
    /// Conversion de sanic_newvalue (texto plano) al valor tipado segun
    /// sanic_datatype del catalogo (sanic_chtblclienttablelogicalfieldname). Este
    /// switch vivia duplicado en dos flujos de Power Automate (Apply/Create,
    /// docs/clientadmin-rebuild/00-analisis-y-plan.md hallazgo L6) con bugs silenciosos
    /// (moneda sin parsear se aplicaba en 0, un option set desconocido no fallaba). Aca
    /// cada rama tiene su caso de exito y su caso de error explicito.
    /// </summary>
    public class FieldValueConverterTests
    {
        private const string LookupTable = "sanic_chtblclientbranch";

        // ---- Text ----

        [Fact]
        public void Text_AnyString_ConvertsAsIs()
        {
            var result = Convert(Text, "Sucursal Centro", null, null);

            Assert.True(result.Success);
            Assert.Equal(ConvertedValueKind.Text, result.Value!.Kind);
            Assert.Equal("Sucursal Centro", result.Value.Text);
        }

        [Fact]
        public void Text_EmptyString_ConvertsAsEmpty()
        {
            var result = Convert(Text, "", null, null);

            Assert.True(result.Success);
            Assert.Equal(string.Empty, result.Value!.Text);
        }

        [Fact]
        public void NullRawValue_IsAlwaysAnError()
        {
            var result = Convert(Text, null, null, null);

            Assert.False(result.Success);
            Assert.NotNull(result.Error);
        }

        // ---- Number / Money (mismo parseo decimal) ----

        [Theory]
        [InlineData(Number)]
        [InlineData(Money)]
        public void NumberOrMoney_ParseableDecimal_Converts(int dataType)
        {
            var result = Convert(dataType, "1234.56", null, null);

            Assert.True(result.Success);
            Assert.Equal(ConvertedValueKind.Decimal, result.Value!.Kind);
            Assert.Equal(1234.56m, result.Value.Decimal);
        }

        [Theory]
        [InlineData(Number)]
        [InlineData(Money)]
        public void NumberOrMoney_NonNumeric_Fails(int dataType)
        {
            var result = Convert(dataType, "no-es-un-numero", null, null);

            Assert.False(result.Success);
            Assert.Contains("numero", result.Error, StringComparison.OrdinalIgnoreCase);
        }

        // ---- Date ----

        [Fact]
        public void Date_ExpectedFormat_Converts()
        {
            var result = Convert(Date, "2026-08-24", null, null);

            Assert.True(result.Success);
            Assert.Equal(ConvertedValueKind.Date, result.Value!.Kind);
            Assert.Equal(new DateTime(2026, 8, 24), result.Value.Date);
        }

        [Theory]
        [InlineData("24/08/2026")]
        [InlineData("2026-13-40")]
        [InlineData("no-es-una-fecha")]
        public void Date_UnexpectedFormat_Fails(string raw)
        {
            var result = Convert(Date, raw, null, null);

            Assert.False(result.Success);
            Assert.Contains("fecha", result.Error, StringComparison.OrdinalIgnoreCase);
        }

        // ---- LookUp ----

        [Fact]
        public void LookUp_ValidGuidWithTargetTable_Converts()
        {
            var id = Guid.NewGuid();

            var result = Convert(LookUp, id.ToString(), null, LookupTable);

            Assert.True(result.Success);
            Assert.Equal(ConvertedValueKind.Lookup, result.Value!.Kind);
            Assert.Equal(id, result.Value.Lookup!.Value.Id);
            Assert.Equal(LookupTable, result.Value.Lookup.Value.LogicalName);
        }

        [Fact]
        public void LookUp_InvalidGuid_Fails()
        {
            var result = Convert(LookUp, "no-es-un-guid", null, LookupTable);

            Assert.False(result.Success);
            Assert.Contains("GUID", result.Error, StringComparison.OrdinalIgnoreCase);
        }

        [Fact]
        public void LookUp_WithoutTargetTableInCatalog_Fails()
        {
            var result = Convert(LookUp, Guid.NewGuid().ToString(), null, null);

            Assert.False(result.Success);
            Assert.Contains("tabla objetivo", result.Error, StringComparison.OrdinalIgnoreCase);
        }

        // ---- OptionSet ----

        [Fact]
        public void OptionSet_LabelInMap_ResolvesCode()
        {
            var map = "{\"Activo\":\"1\",\"Inactivo\":\"0\"}";

            var result = Convert(OptionSet, "Activo", map, null);

            Assert.True(result.Success);
            Assert.Equal(ConvertedValueKind.OptionSet, result.Value!.Kind);
            Assert.Equal(1, result.Value.OptionSetCode);
        }

        [Fact]
        public void OptionSet_MissingMap_Fails()
        {
            var result = Convert(OptionSet, "Activo", null, null);

            Assert.False(result.Success);
            Assert.Contains("mapa de opciones", result.Error, StringComparison.OrdinalIgnoreCase);
        }

        [Fact]
        public void OptionSet_LabelNotInMap_Fails()
        {
            var map = "{\"Activo\":\"1\"}";

            var result = Convert(OptionSet, "Inexistente", map, null);

            Assert.False(result.Success);
            Assert.Contains("etiqueta", result.Error, StringComparison.OrdinalIgnoreCase);
        }

        [Fact]
        public void OptionSet_InvalidJsonMap_FailsWithoutThrowing()
        {
            var result = Convert(OptionSet, "Activo", "{esto no es json", null);

            Assert.False(result.Success);
        }

        // ---- TwoOptions ----

        [Theory]
        [InlineData("yes", true)]
        [InlineData("YES", true)]
        [InlineData("true", true)]
        [InlineData("TRUE", true)]
        [InlineData("no", false)]
        [InlineData("false", false)]
        public void TwoOptions_RecognizedTokens_Converts(string raw, bool expected)
        {
            var result = Convert(TwoOptions, raw, null, null);

            Assert.True(result.Success);
            Assert.Equal(ConvertedValueKind.Boolean, result.Value!.Kind);
            Assert.Equal(expected, result.Value.Boolean);
        }

        [Fact]
        public void TwoOptions_UnrecognizedToken_Fails()
        {
            var result = Convert(TwoOptions, "tal-vez", null, null);

            Assert.False(result.Success);
        }

        // ---- Tipo desconocido ----

        [Fact]
        public void UnknownDataType_Fails()
        {
            var result = Convert(999999, "x", null, null);

            Assert.False(result.Success);
        }
    }
}
