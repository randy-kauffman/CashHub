using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace CashHub.Validation.Export
{
    /// <summary>
    /// Lista blanca de caracteres para los campos de texto que van al CSV del core.
    ///
    /// POR QUE EXISTE. El CSV al core sale separado por comas y SIN entrecomillar nada.
    /// Un depositante llamado "PEREZ, JUAN" parte la fila en dos y corre TODAS las
    /// columnas siguientes una posicion: MONEDA se lleva el valor de DEPOSITID,
    /// EFECTIVO se lleva otro. La fila no revienta —sigue pareciendo valida— y postea
    /// mal. Un archivo que falla se ve; uno que postea mal no se ve hasta que alguien
    /// cuadra. Un salto de linea es todavia peor: parte la fila en DOS FILAS.
    ///
    /// UNA REGLA, DOS DEFENSAS. La lista vive en la tabla de parametros
    /// (Export.Core.AllowedCharacters) y la comparten:
    ///   - la EXPORTACION, que sanea lo que ya esta guardado (<see cref="Sanitize"/>);
    ///   - la VALIDACION DE ENTRADA, que rebota en origen lo que no cumple
    ///     (<see cref="IsClean"/> / <see cref="ForbiddenCharacters"/>).
    /// Sanear al exportar no sobra aunque la validacion rebote: los datos viejos ya
    /// estan adentro, y una defensa que depende de que la otra haya corrido no es
    /// defensa en profundidad.
    ///
    /// LISTA BLANCA, NO LISTA NEGRA. Se declara lo que SI puede pasar. Una lista negra
    /// obliga a acertarle a todos los caracteres peligrosos de antemano; la blanca
    /// falla del lado seguro ante lo que nadie previo.
    /// </summary>
    public static class CoreExportSanitizer
    {
        /// <summary>
        /// Devuelve el texto con todo caracter fuera de la lista reemplazado por un
        /// espacio, colapsando espacios repetidos y recortando las puntas.
        ///
        /// Reemplaza por espacio en vez de borrar porque "PEREZ,JUAN" sin espacio se
        /// convertiria en "PEREZJUAN": pegar dos palabras corrompe el nombre mas que
        /// separarlas.
        ///
        /// DEFAULT-DENY: lista vacia o nula significa "no pasa nada", no "pasa todo".
        /// Si fuera al reves, borrar el parametro por error apagaria la defensa justo
        /// cuando mas hace falta.
        /// </summary>
        public static string Sanitize(string value, string allowed)
        {
            if (string.IsNullOrEmpty(value))
            {
                return string.Empty;
            }

            var permitidos = BuildSet(allowed);
            var sb = new StringBuilder(value.Length);
            var espaciopendiente = false;

            foreach (var c in value)
            {
                if (permitidos.Contains(c) && c != ' ')
                {
                    if (espaciopendiente && sb.Length > 0)
                    {
                        sb.Append(' ');
                    }
                    espaciopendiente = false;
                    sb.Append(c);
                }
                else
                {
                    // Tanto el caracter prohibido como el espacio legitimo se vuelven
                    // "hay separacion aca", y recien se materializa si despues viene
                    // algo. Asi se colapsa y se recorta en una sola pasada.
                    espaciopendiente = true;
                }
            }

            return sb.ToString();
        }

        /// <summary>
        /// True si el texto no tiene ningun caracter fuera de la lista. Lo que la
        /// validacion de entrada usa para REBOTAR en origen en vez de sanear.
        /// </summary>
        public static bool IsClean(string value, string allowed) =>
            ForbiddenCharacters(value, allowed).Length == 0;

        /// <summary>
        /// Los caracteres prohibidos que aparecen en el texto, sin repetir y en orden de
        /// aparicion. Existe para poder NOMBRARLOS en el error: "hay un caracter no
        /// permitido" no le sirve a nadie; el operador necesita saber cual sacar.
        /// </summary>
        public static char[] ForbiddenCharacters(string value, string allowed)
        {
            if (string.IsNullOrEmpty(value))
            {
                return new char[0];
            }

            var permitidos = BuildSet(allowed);
            var vistos = new HashSet<char>();
            var fuera = new List<char>();

            foreach (var c in value.Where(c => !permitidos.Contains(c)))
            {
                if (vistos.Add(c))
                {
                    fuera.Add(c);
                }
            }

            return fuera.ToArray();
        }

        private static HashSet<char> BuildSet(string allowed) =>
            string.IsNullOrEmpty(allowed) ? new HashSet<char>() : new HashSet<char>(allowed);
    }
}
