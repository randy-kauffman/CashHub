namespace CashHub.Validation.Rules
{
    /// <summary>
    /// Contrato con la plantilla CH-TPL-Depositos v1.1.0: nombres de tablas y columnas.
    /// Si la plantilla cambia de forma, cambia aquí (y sube TemplateVersion en VR-27).
    /// </summary>
    public static class TemplateContract
    {
        public const string DepositsTable = "Depositos";
        public const string ChecksTable = "Cheques";

        public static class Deposits
        {
            public const string DepositNo = "No. deposito";
            public const string BagNo = "No. bolsa";
            public const string AccountNo = "No. Cuenta";
            public const string Currency = "Moneda";
            public const string BacChecks = "Cheques Locales BAC";
            public const string OtherChecks = "Cheques Locales Otros";
            public const string Cash = "Efectivo";
            public const string Total = "Total";
            public const string Reference = "Referencia";

            public static readonly string[] NumericColumns =
                { DepositNo, BacChecks, OtherChecks, Cash, Total };
        }

        public static class Checks
        {
            public const string DepositNo = "No. Deposito";
            public const string CheckNo = "No. Cheque";
            public const string Type = "Tipo";
            public const string Currency = "Moneda";
            public const string Amount = "Monto";

            public static readonly string[] NumericColumns = { DepositNo, Amount };
        }
    }
}
