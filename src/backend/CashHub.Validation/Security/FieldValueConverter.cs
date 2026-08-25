using System;
using System.Globalization;

namespace CashHub.Validation.Security
{
    /// <summary>Forma del valor convertido — el plugin la usa para armar el atributo del SDK.</summary>
    public enum ConvertedValueKind
    {
        Text,
        Decimal,
        Date,
        Lookup,
        OptionSet,
        Boolean
    }

    /// <summary>Referencia a un registro (tabla + id) para un campo LookUp del catalogo.</summary>
    public readonly struct LookupReference : IEquatable<LookupReference>
    {
        public LookupReference(string logicalName, Guid id)
        {
            LogicalName = logicalName;
            Id = id;
        }

        /// <summary>sanic_lookuptargettable del catalogo.</summary>
        public string LogicalName { get; }

        public Guid Id { get; }

        public bool Equals(LookupReference other) =>
            string.Equals(LogicalName, other.LogicalName, StringComparison.OrdinalIgnoreCase) && Id == other.Id;

        public override bool Equals(object? obj) => obj is LookupReference other && Equals(other);

        public override int GetHashCode()
        {
            unchecked
            {
                var hash = 17;
                hash = hash * 31 + (LogicalName?.ToLowerInvariant().GetHashCode() ?? 0);
                hash = hash * 31 + Id.GetHashCode();
                return hash;
            }
        }
    }

    /// <summary>
    /// Valor ya tipado segun sanic_datatype. Solo la propiedad que corresponde a
    /// <see cref="Kind"/> es significativa — el plugin la lee segun ese discriminante,
    /// nunca "adivina" cual esta poblada.
    /// </summary>
    public sealed class ConvertedValue
    {
        private ConvertedValue(ConvertedValueKind kind, string? text, decimal @decimal, DateTime date,
            LookupReference? lookup, int optionSetCode, bool boolean)
        {
            Kind = kind;
            Text = text;
            Decimal = @decimal;
            Date = date;
            Lookup = lookup;
            OptionSetCode = optionSetCode;
            Boolean = boolean;
        }

        public ConvertedValueKind Kind { get; }
        public string? Text { get; }
        public decimal Decimal { get; }
        public DateTime Date { get; }
        public LookupReference? Lookup { get; }
        public int OptionSetCode { get; }
        public bool Boolean { get; }

        public static ConvertedValue OfText(string value) =>
            new ConvertedValue(ConvertedValueKind.Text, value, 0m, default, null, 0, false);

        public static ConvertedValue OfDecimal(decimal value) =>
            new ConvertedValue(ConvertedValueKind.Decimal, null, value, default, null, 0, false);

        public static ConvertedValue OfDate(DateTime value) =>
            new ConvertedValue(ConvertedValueKind.Date, null, 0m, value, null, 0, false);

        public static ConvertedValue OfLookup(LookupReference value) =>
            new ConvertedValue(ConvertedValueKind.Lookup, null, 0m, default, value, 0, false);

        public static ConvertedValue OfOptionSet(int code) =>
            new ConvertedValue(ConvertedValueKind.OptionSet, null, 0m, default, null, code, false);

        public static ConvertedValue OfBoolean(bool value) =>
            new ConvertedValue(ConvertedValueKind.Boolean, null, 0m, default, null, 0, value);
    }

    /// <summary>Resultado de convertir UN sanic_newvalue. Nunca lanza: el error es siempre de negocio.</summary>
    public sealed class FieldConversionResult
    {
        private FieldConversionResult(bool success, ConvertedValue? value, string? error)
        {
            Success = success;
            Value = value;
            Error = error;
        }

        public bool Success { get; }
        public ConvertedValue? Value { get; }
        public string? Error { get; }

        public static FieldConversionResult Ok(ConvertedValue value) => new FieldConversionResult(true, value, null);

        public static FieldConversionResult Fail(string error) => new FieldConversionResult(false, null, error);
    }

    /// <summary>
    /// Convierte sanic_newvalue (texto plano, sin tipar) al valor tipado que indica
    /// sanic_datatype del catalogo sanic_chtblclienttablelogicalfieldname. Reemplaza el
    /// switch de conversion duplicado 1:1 entre los flujos Apply/Create de Power Automate
    /// (docs/clientadmin-rebuild/00-analisis-y-plan.md hallazgo L6), donde un valor no
    /// parseable no fallaba — se aplicaba silenciosamente en blanco o en cero. Aca CADA
    /// rama es una decision explicita: exito tipado o error de negocio con el motivo.
    /// </summary>
    public static class FieldValueConverter
    {
        // Option set sanic_datatype (verificado contra el Entity.xml del catalogo).
        public const int Text = 159460000;
        public const int Number = 159460001;
        public const int Date = 159460002;
        public const int Money = 159460003;
        public const int LookUp = 159460004;
        public const int OptionSet = 159460005;
        public const int TwoOptions = 159460006;

        private const string DateFormat = "yyyy-MM-dd";

        /// <summary>
        /// Convierte <paramref name="rawValue"/> (sanic_newvalue) segun
        /// <paramref name="dataType"/> (sanic_datatype de la linea del catalogo).
        /// </summary>
        /// <param name="optionSetMapJson">sanic_optionsetmap del catalogo — solo se usa para OptionSet.</param>
        /// <param name="lookupTargetTable">sanic_lookuptargettable del catalogo — solo se usa para LookUp.</param>
        public static FieldConversionResult Convert(
            int dataType, string? rawValue, string? optionSetMapJson, string? lookupTargetTable)
        {
            if (rawValue == null)
            {
                return FieldConversionResult.Fail(
                    "El nuevo valor de la linea (sanic_newvalue) esta vacio; no hay nada que convertir.");
            }

            switch (dataType)
            {
                case Text:
                    return FieldConversionResult.Ok(ConvertedValue.OfText(rawValue));

                case Number:
                case Money:
                    return ConvertDecimal(rawValue);

                case Date:
                    return ConvertDate(rawValue);

                case LookUp:
                    return ConvertLookup(rawValue, lookupTargetTable);

                case OptionSet:
                    return ConvertOptionSet(rawValue, optionSetMapJson);

                case TwoOptions:
                    return ConvertBoolean(rawValue);

                default:
                    return FieldConversionResult.Fail($"Tipo de dato de campo desconocido: {dataType}.");
            }
        }

        private static FieldConversionResult ConvertDecimal(string rawValue)
        {
            if (!decimal.TryParse(rawValue, NumberStyles.Number, CultureInfo.InvariantCulture, out var value))
            {
                return FieldConversionResult.Fail(
                    $"'{rawValue}' no es un numero valido para un campo Number/Money.");
            }

            return FieldConversionResult.Ok(ConvertedValue.OfDecimal(value));
        }

        private static FieldConversionResult ConvertDate(string rawValue)
        {
            if (!DateTime.TryParseExact(
                    rawValue, DateFormat, CultureInfo.InvariantCulture, DateTimeStyles.None, out var value))
            {
                return FieldConversionResult.Fail(
                    $"'{rawValue}' no tiene el formato de fecha esperado ({DateFormat}).");
            }

            return FieldConversionResult.Ok(ConvertedValue.OfDate(value));
        }

        private static FieldConversionResult ConvertLookup(string rawValue, string? lookupTargetTable)
        {
            if (!Guid.TryParse(rawValue, out var id))
            {
                return FieldConversionResult.Fail(
                    $"'{rawValue}' no es un GUID valido para un campo LookUp.");
            }

            if (string.IsNullOrWhiteSpace(lookupTargetTable))
            {
                return FieldConversionResult.Fail(
                    "El catalogo no define la tabla objetivo del lookup (sanic_lookuptargettable); " +
                    "no se puede armar la referencia.");
            }

            return FieldConversionResult.Ok(ConvertedValue.OfLookup(new LookupReference(lookupTargetTable!, id)));
        }

        private static FieldConversionResult ConvertOptionSet(string rawValue, string? optionSetMapJson)
        {
            if (string.IsNullOrWhiteSpace(optionSetMapJson))
            {
                return FieldConversionResult.Fail(
                    "El campo del catalogo no tiene configurado el mapa de opciones (sanic_optionsetmap); " +
                    "no se puede resolver el codigo.");
            }

            // FlatJson.Value nunca lanza: un JSON invalido o truncado devuelve null, igual
            // que una etiqueta ausente — las dos caras del mismo error de negocio (mapa mal
            // configurado), sin necesidad de un parser JSON completo en el assembly net462.
            var rawCode = FlatJson.Value(optionSetMapJson, rawValue);
            if (rawCode == null)
            {
                return FieldConversionResult.Fail(
                    $"La etiqueta '{rawValue}' no existe en el mapa de opciones del campo (o el mapa esta mal formado).");
            }

            if (!int.TryParse(rawCode, NumberStyles.Integer, CultureInfo.InvariantCulture, out var code))
            {
                return FieldConversionResult.Fail(
                    $"El mapa de opciones tiene un codigo invalido para la etiqueta '{rawValue}': '{rawCode}'.");
            }

            return FieldConversionResult.Ok(ConvertedValue.OfOptionSet(code));
        }

        private static FieldConversionResult ConvertBoolean(string rawValue)
        {
            var trimmed = rawValue.Trim();

            if (string.Equals(trimmed, "yes", StringComparison.OrdinalIgnoreCase)
                || string.Equals(trimmed, "true", StringComparison.OrdinalIgnoreCase))
            {
                return FieldConversionResult.Ok(ConvertedValue.OfBoolean(true));
            }

            if (string.Equals(trimmed, "no", StringComparison.OrdinalIgnoreCase)
                || string.Equals(trimmed, "false", StringComparison.OrdinalIgnoreCase))
            {
                return FieldConversionResult.Ok(ConvertedValue.OfBoolean(false));
            }

            return FieldConversionResult.Fail(
                $"'{rawValue}' no es un valor booleano reconocido para TwoOptions (yes/no, true/false).");
        }
    }
}
