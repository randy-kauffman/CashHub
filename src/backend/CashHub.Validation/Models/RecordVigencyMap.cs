using System.Collections.Generic;

namespace CashHub.Validation.Models
{
    /// <summary>
    /// UNICA fuente de verdad de "que cuenta como vigente" en las tablas maestras de
    /// cliente (Client / ClientBranch / ClientEmail / ClientAccount, todas con la columna
    /// gemela sanic_recordstatus sobre el option set global sanic_chchrecordstatus).
    ///
    /// Vive en el dominio y no en la capa Dataverse por dos razones:
    ///
    /// 1) La regla se escribia DOS veces en ReceivedEmailValidationService — una al traducir
    ///    la fila leida y otra al armar el filtro de la consulta. Dos copias de una regla
    ///    divergen; cuando divergen, una consulta trae filas que la traduccion despues
    ///    rechaza (o peor, al reves) y vuelve el bypass por otra puerta.
    ///
    /// 2) CashHub.Plugins es net462 y Microsoft.CrmSdk.CoreAssemblies solo publica net462,
    ///    asi que un proyecto de tests net8.0 no puede referenciarlo. Toda decision que
    ///    quede alli es intesteable. Aca si se prueba.
    /// </summary>
    public static class RecordVigencyMap
    {
        // Valores del option set global sanic_chchrecordstatus.
        public const int Active = 159460000;
        public const int Inactive = 159460001;
        public const int PendingApproval = 159460002;
        public const int PendingDeactivation = 159460003;

        /// <summary>
        /// Los valores que una consulta debe traer para considerar el registro operativo.
        /// El filtro de Dataverse se arma de aca, asi que no puede desincronizarse de
        /// <see cref="FromRecordStatus"/> — hay un test que lo verifica.
        /// </summary>
        public static IReadOnlyList<int> VigentValues { get; } = new[] { Active, PendingDeactivation };

        /// <summary>
        /// Traduce sanic_recordstatus a vigencia.
        ///
        /// PendingDeactivation es VIGENTE: la baja se pidio pero el aprobador no firmo,
        /// y hasta que firme el registro sigue operando.
        ///
        /// PendingApproval NO es vigente: es el AppDefaultValue de un registro recien
        /// creado, un alta esperando firma. Se trata igual que Inactive por decision de
        /// negocio — mientras no este resuelto, no autoriza.
        ///
        /// Cualquier otro valor (incluido null) tampoco autoriza: sin evidencia de
        /// vigencia no se otorga confianza. Falla cerrado, siempre.
        /// </summary>
        public static Vigency FromRecordStatus(int? recordStatus)
        {
            switch (recordStatus)
            {
                case Active:
                case PendingDeactivation:
                    return Vigency.Vigente;
                case PendingApproval:
                    return Vigency.Pendiente;
                default:
                    return Vigency.Revocado;
            }
        }

        /// <summary>¿Este estado habilita operar? Un solo lugar donde se contesta.</summary>
        public static bool IsVigent(Vigency vigency) => vigency == Vigency.Vigente;
    }
}
