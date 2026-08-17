using System;
using CashHub.Validation.Security;
using Microsoft.Xrm.Sdk;
using Microsoft.Xrm.Sdk.Query;

namespace CashHub.Plugins
{
    /// <summary>
    /// Espejo E-3 de CH-App-Recounting v2 (01 §2.2):
    ///
    ///   sanic_declaredbagmirror (lookup -> sanic_chtbldeclaredbags) en
    ///   sanic_chtbldeclaredchecks, derivado de
    ///   sanic_declareddepositid.sanic_declaredbag.
    ///
    /// Dos pasos SINCRONOS, los dos Pre-operation (20) sobre el cheque declarado:
    ///   - Create de sanic_chtbldeclaredchecks: estampa el espejo.
    ///   - Update de sanic_chtbldeclaredchecks, filtrado en sanic_declareddepositid:
    ///     re-estampa al re-colgar el cheque declarado de otro deposito declarado.
    ///
    /// Se escribe EN el Target: UNA sola lectura del deposito declarado y CERO
    /// escrituras adicionales. La lectura va como sistema porque es una derivacion, no
    /// un permiso de negocio.
    ///
    /// Por que existe: la apertura de tula tiene que traer TODOS los cheques
    /// declarados de la bolsa. Sin el espejo hay que hacer un Filter remoto POR
    /// DEPOSITO DECLARADO (medidos ~82 round-trips en una apertura). Con la columna en
    /// el propio cheque declarado, es UNA query delegable.
    ///
    /// -------------------------------------------------------------------------
    /// LIMITACION CONOCIDA — NO HAY CASCADA DESDE EL DEPOSITO DECLARADO
    /// -------------------------------------------------------------------------
    /// Si a un deposito declarado le cambiaran sanic_declaredbag, sus cheques
    /// declarados YA existentes quedarian con la bolsa vieja en el espejo. Es
    /// deliberado y esta respaldado por el codigo: DeclarationMaterializer materializa
    /// la cadena Declaration -> DeclaredBags -> DeclaredDeposits -> DeclaredChecks en
    /// ese orden y escribe sanic_declaredbag en el CREATE del deposito; para cuando
    /// nace el cheque declarado, su deposito ya trae la bolsa. Nadie re-parenta un
    /// deposito declarado despues (la declaracion es inmutable: si el correo se
    /// re-procesa, se materializa una declaracion nueva).
    /// La cobertura, si algun dia pasa, es scripts/backfill-recounting-mirrors.py
    /// --only e3: es re-ejecutable e idempotente. Si el re-parenteo se volviera real,
    /// el reemplazo natural es un paso Post-operation (40) de Update de
    /// sanic_chtbldeclareddeposits filtrado en sanic_declaredbag con PreImage,
    /// paginado — mismo patron que la cascada de DepositSessionMirrorPlugin.
    /// </summary>
    public sealed class DeclaredCheckBagMirrorPlugin : IPlugin
    {
        public void Execute(IServiceProvider serviceProvider)
        {
            var context = (IPluginExecutionContext)serviceProvider.GetService(typeof(IPluginExecutionContext));
            var serviceFactory = (IOrganizationServiceFactory)serviceProvider.GetService(typeof(IOrganizationServiceFactory));
            var tracing = (ITracingService)serviceProvider.GetService(typeof(ITracingService));

            tracing.Trace("DeclaredCheckBagMirror: {0} de {1}, depth={2}.",
                context.MessageName, context.PrimaryEntityName, context.Depth);

            if (!(context.InputParameters.TryGetValue("Target", out var raw) && raw is Entity target))
            {
                tracing.Trace("DeclaredCheckBagMirror: sin Target de tipo Entity, se omite.");
                return;
            }

            if (context.PrimaryEntityName != Schema.DeclaredCheck.LogicalName)
            {
                tracing.Trace("DeclaredCheckBagMirror: entidad {0} fuera de alcance, se omite.",
                    context.PrimaryEntityName);
                return;
            }

            try
            {
                StampDeclaredBag(
                    serviceFactory.CreateOrganizationService(null),
                    tracing, context.MessageName, target);
            }
            catch (InvalidPluginExecutionException)
            {
                throw;
            }
            catch (Exception ex)
            {
                tracing.Trace("DeclaredCheckBagMirror: error inesperado en {0} de {1}: {2}",
                    context.MessageName, context.PrimaryEntityName, ex);
                throw new InvalidPluginExecutionException(
                    "No se pudo mantener el espejo de la bolsa declarada en el cheque declarado (E-3): "
                    + ex.Message, ex);
            }
        }

        private static void StampDeclaredBag(
            IOrganizationService system, ITracingService tracing, string message, Entity target)
        {
            var depositTouched = target.Contains(Schema.DeclaredCheck.DeclaredDepositLookup);

            if (!RecountingMirrors.ShouldStamp(message, depositTouched))
            {
                tracing.Trace(
                    "DeclaredCheckBagMirror: Update de cheque declarado sin cambio de deposito, "
                    + "sin re-estampa.");
                return;
            }

            var depositRef = target.GetAttributeValue<EntityReference>(
                Schema.DeclaredCheck.DeclaredDepositLookup);

            if (depositRef == null)
            {
                // Sin deposito declarado no hay bolsa posible: blanco EXPLICITO.
                target[Schema.DeclaredCheck.DeclaredBagMirror] = null;
                tracing.Trace(
                    "DeclaredCheckBagMirror: cheque declarado sin deposito, espejo en blanco.");
                return;
            }

            var deposit = system.Retrieve(
                Schema.DeclaredDeposit.LogicalName, depositRef.Id,
                new ColumnSet(Schema.DeclaredDeposit.DeclaredBagLookup));

            var bagRef = deposit.GetAttributeValue<EntityReference>(
                Schema.DeclaredDeposit.DeclaredBagLookup);

            // Deposito SIN bolsa tambien es un valor: null limpio, nunca el anterior.
            target[Schema.DeclaredCheck.DeclaredBagMirror] = bagRef;

            tracing.Trace(
                "DeclaredCheckBagMirror: cheque declarado {0} ({1}) -> deposito {2}, bolsa {3}.",
                target.Id, message, depositRef.Id,
                bagRef != null ? bagRef.Id.ToString() : "null");
        }
    }
}
