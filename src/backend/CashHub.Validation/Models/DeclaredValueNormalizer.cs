namespace CashHub.Validation.Models
{
    /// <summary>
    /// Normaliza los valores de la declaracion ANTES de validarlos y de persistirlos.
    ///
    /// Fuente unica a proposito: si VR-29 valida el valor crudo y DeclarationMaterializer
    /// guarda otro, el motor valida una cosa y persiste otra. Vive en el dominio (y no en
    /// el materializador) porque CashHub.Plugins es net462 y CrmSdk.CoreAssemblies solo
    /// publica net462: lo que quede alli no se puede testear.
    /// </summary>
    public static class DeclaredValueNormalizer
    {
        /// <summary>
        /// Numero de tula tal como se guarda: sin espacios alrededor y en MAYUSCULAS.
        ///
        /// Antes se agrupaba con OrdinalIgnoreCase pero se persistia la capitalizacion de
        /// la PRIMERA fila del grupo, asi que la misma tula quedaba escrita distinto segun
        /// el orden de las filas del Excel.
        ///
        /// ToUpperInvariant, no ToUpper: con cultura turca 'i' se convierte en 'İ' y el
        /// numero de tula cambiaria segun donde corra el plugin.
        /// </summary>
        public static string BagNumber(string? raw) =>
            raw == null ? string.Empty : raw.Trim().ToUpperInvariant();

        /// <summary>
        /// Numero de cheque tal como se guarda: solo se le sacan los espacios alrededor.
        ///
        /// No se mayusculiza — VR-29 garantiza que sean solo digitos, y si alguna vez
        /// entrara una letra, mayusculizarla la escondaria en lugar de rechazarla. Se
        /// guarda como TEXTO porque los ceros a la izquierda son significativos: 0001234
        /// y 1234 son cheques distintos.
        /// </summary>
        public static string CheckNumber(string? raw) =>
            raw == null ? string.Empty : raw.Trim();

        /// <summary>
        /// true si el numero de cheque normalizado son solo digitos ASCII (0-9), no vacio.
        ///
        /// char.IsDigit NO sirve: devuelve true para los digitos arabigo-indicos (١٢٣) y
        /// para los de otros sistemas Unicode, que no son lo que hay en una linea MICR.
        /// </summary>
        public static bool IsValidCheckNumber(string? raw)
        {
            var value = CheckNumber(raw);
            if (value.Length == 0) return false;

            foreach (var c in value)
            {
                if (c < '0' || c > '9') return false;
            }
            return true;
        }
    }
}
