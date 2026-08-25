using System;
using CashHub.Validation.Security;
using Xunit;
using static CashHub.Validation.Security.ChangeRequestApprovalGuard;
using static CashHub.Validation.Security.ChangeRequestResolution;

namespace CashHub.Validation.Tests
{
    /// <summary>
    /// Motor de decision de la Custom API sanic_CHResolveChangeRequest: reemplaza los 4
    /// flujos materializadores. Reutiliza <see cref="ChangeRequestApprovalGuard"/> para la
    /// separacion de funciones (creador != resolutor, membresia Approver) y agrega el
    /// mapeo a los estados FINALES de cabecera y linea (Applied/Rejected+Denied) que antes
    /// vivian repartidos entre 4 flujos de Power Automate.
    /// </summary>
    public class ChangeRequestResolutionTests
    {
        private static readonly Guid Creator = Guid.Parse("11111111-1111-1111-1111-111111111111");
        private static readonly Guid Approver = Guid.Parse("22222222-2222-2222-2222-222222222222");

        private const bool IsApprover = true;
        private const bool NotApprover = false;

        [Fact]
        public void Approve_Submitted_ByAuthorizedApprover_ReturnsAppliedHeaderAndLine()
        {
            var result = Evaluate(Submitted, ChangeRequestDecision.Approve, Creator, Approver, IsApprover);

            Assert.True(result.IsAllowed);
            Assert.Equal(Applied, result.HeaderStatus);
            Assert.Equal(LineApplied, result.LineStatus);
        }

        [Fact]
        public void Reject_Submitted_ByAuthorizedApprover_ReturnsRejectedHeaderAndDeniedLine()
        {
            var result = Evaluate(Submitted, ChangeRequestDecision.Reject, Creator, Approver, IsApprover);

            Assert.True(result.IsAllowed);
            Assert.Equal(Rejected, result.HeaderStatus);
            Assert.Equal(LineDenied, result.LineStatus);
        }

        [Fact]
        public void Approve_ByCreator_IsBlocked()
        {
            // Mismo agujero que ChangeRequestApprovalGuard: el creador no se auto-resuelve.
            var result = Evaluate(Submitted, ChangeRequestDecision.Approve, Creator, Creator, IsApprover);

            Assert.False(result.IsAllowed);
            Assert.Contains("separacion de funciones", result.Reason);
        }

        [Fact]
        public void Approve_ByNonApprover_IsBlocked()
        {
            var result = Evaluate(Submitted, ChangeRequestDecision.Approve, Creator, Approver, NotApprover);

            Assert.False(result.IsAllowed);
            Assert.Contains("grupo de aprobadores", result.Reason);
        }

        [Theory]
        [InlineData(Draft)]
        [InlineData(Approved)]
        [InlineData(Rejected)]
        [InlineData(Applied)]
        [InlineData(Failed)]
        [InlineData(Canceled)]
        public void Resolve_FromNonSubmittedState_IsBlocked(int currentStatus)
        {
            var result = Evaluate(currentStatus, ChangeRequestDecision.Approve, Creator, Approver, IsApprover);

            Assert.False(result.IsAllowed);
        }

        [Theory]
        [InlineData(TypeUpdate, true)]
        [InlineData(TypeCreate, true)]
        [InlineData(TypeActivate, false)]
        [InlineData(TypeDeactivate, false)]
        public void RequiresLines_UpdateAndCreateOnly(int type, bool expected)
        {
            Assert.Equal(expected, RequiresLines(type));
        }
    }
}
