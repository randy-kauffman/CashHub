using System;
using CashHub.Validation.Security;
using Xunit;
using static CashHub.Validation.Security.ChangeRequestApprovalGuard;

namespace CashHub.Validation.Tests
{
    /// <summary>
    /// Especificacion de la separacion de funciones de los RFC de administracion de clientes.
    /// El agujero que cierra: un Contributor pegandole Approved a su propio RFC por fuera de la app.
    /// </summary>
    public class ChangeRequestApprovalGuardTests
    {
        private static readonly Guid Creator = Guid.Parse("11111111-1111-1111-1111-111111111111");
        private static readonly Guid Approver = Guid.Parse("22222222-2222-2222-2222-222222222222");

        private const bool IsApprover = true;
        private const bool NotApprover = false;

        // ---- Update: la decision humana (aprobar/rechazar) ----

        [Fact]
        public void Approve_Submitted_ByAuthorizedApprover_IsAllowed()
        {
            var result = Evaluate(ChangeRequestEvent.Update, Submitted, Approved, Creator, Approver, IsApprover);
            Assert.True(result.IsAllowed);
        }

        [Fact]
        public void Reject_Submitted_ByAuthorizedApprover_IsAllowed()
        {
            var result = Evaluate(ChangeRequestEvent.Update, Submitted, Rejected, Creator, Approver, IsApprover);
            Assert.True(result.IsAllowed);
        }

        [Fact]
        public void Approve_Submitted_ByCreator_IsBlocked()
        {
            // El ataque central: el creador se auto-aprueba (aunque fuese aprobador de la tabla).
            var result = Evaluate(ChangeRequestEvent.Update, Submitted, Approved, Creator, Creator, IsApprover);
            Assert.False(result.IsAllowed);
            Assert.Contains("separacion de funciones", result.Reason);
        }

        [Fact]
        public void Reject_Submitted_ByCreator_IsBlocked()
        {
            var result = Evaluate(ChangeRequestEvent.Update, Submitted, Rejected, Creator, Creator, IsApprover);
            Assert.False(result.IsAllowed);
        }

        [Fact]
        public void Approve_Submitted_ByNonApprover_IsBlocked()
        {
            // Fase 2: un usuario distinto del creador pero que NO esta en el grupo Approver de la tabla.
            var result = Evaluate(ChangeRequestEvent.Update, Submitted, Approved, Creator, Approver, NotApprover);
            Assert.False(result.IsAllowed);
            Assert.Contains("grupo de aprobadores", result.Reason);
        }

        [Fact]
        public void Reject_Submitted_ByNonApprover_IsBlocked()
        {
            var result = Evaluate(ChangeRequestEvent.Update, Submitted, Rejected, Creator, Approver, NotApprover);
            Assert.False(result.IsAllowed);
        }

        [Theory]
        [InlineData(Draft)]
        [InlineData(Approved)]
        [InlineData(Rejected)]
        [InlineData(Applied)]
        [InlineData(Failed)]
        [InlineData(Canceled)]
        public void Approve_FromNonSubmittedState_IsBlocked(int previousStatus)
        {
            // Aprobar salteando el estado Submitted no es una transicion valida.
            var result = Evaluate(ChangeRequestEvent.Update, previousStatus, Approved, Creator, Approver, IsApprover);
            Assert.False(result.IsAllowed);
        }

        [Fact]
        public void Approve_WithUnknownCreator_IsBlocked()
        {
            var result = Evaluate(ChangeRequestEvent.Update, Submitted, Approved, Guid.Empty, Approver, IsApprover);
            Assert.False(result.IsAllowed);
        }

        // ---- Update: transiciones de sistema / no-decision (deben pasar) ----

        [Fact]
        public void SystemTransition_ApprovedToApplied_IsAllowed()
        {
            // Lo escribe el flujo con el service principal; no debe bloquearse.
            var result = Evaluate(ChangeRequestEvent.Update, Approved, Applied, Creator, Approver, NotApprover);
            Assert.True(result.IsAllowed);
        }

        [Fact]
        public void SystemTransition_ApprovedToFailed_IsAllowed()
        {
            var result = Evaluate(ChangeRequestEvent.Update, Approved, Failed, Creator, Approver, NotApprover);
            Assert.True(result.IsAllowed);
        }

        [Fact]
        public void SubmitByCreator_DraftToSubmitted_IsAllowed()
        {
            var result = Evaluate(ChangeRequestEvent.Update, Draft, Submitted, Creator, Creator, NotApprover);
            Assert.True(result.IsAllowed);
        }

        [Fact]
        public void CancelByCreator_SubmittedToCanceled_IsAllowed()
        {
            var result = Evaluate(ChangeRequestEvent.Update, Submitted, Canceled, Creator, Creator, NotApprover);
            Assert.True(result.IsAllowed);
        }

        // ---- Create: no se puede parir un RFC ya resuelto ----

        [Theory]
        [InlineData(Draft)]
        [InlineData(Submitted)]
        [InlineData(Canceled)]
        public void Create_InAllowedInitialState_IsAllowed(int newStatus)
        {
            var result = Evaluate(ChangeRequestEvent.Create, null, newStatus, Creator, Creator, NotApprover);
            Assert.True(result.IsAllowed);
        }

        [Theory]
        [InlineData(Approved)]
        [InlineData(Rejected)]
        [InlineData(Applied)]
        [InlineData(Failed)]
        public void Create_InResolvedState_IsBlocked(int newStatus)
        {
            // Cierra el bypass por Create: el Contributor tiene prvCreate.
            var result = Evaluate(ChangeRequestEvent.Create, null, newStatus, Creator, Creator, NotApprover);
            Assert.False(result.IsAllowed);
        }
    }
}
