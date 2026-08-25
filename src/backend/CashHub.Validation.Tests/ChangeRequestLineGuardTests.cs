using System;
using CashHub.Validation.Security;
using Xunit;
using static CashHub.Validation.Security.ChangeRequestApprovalGuard;
using static CashHub.Validation.Security.ChangeRequestLineGuard;

namespace CashHub.Validation.Tests
{
    /// <summary>
    /// Guard de sanic_chtblclientdatachangerequestline (cierra S3 del analisis: crear o
    /// borrar lineas de un RFC ajeno en Draft, por Web API, era posible para cualquiera
    /// con Write global). Reglas: el RFC padre debe estar en Draft, sin duplicados por
    /// (RFC, FieldDefinition), y solo el creador del RFC o un aprobador de la tabla
    /// objetivo puede tocar sus lineas. Las transiciones de sistema (las que hace el
    /// propio ResolveChangeRequestPlugin al aplicar/rechazar, con el RFC ya en Submitted)
    /// se dejan pasar por profundidad de pipeline — mismo patron que RecountingGuard.
    /// </summary>
    public class ChangeRequestLineGuardTests
    {
        private static readonly Guid Creator = Guid.Parse("11111111-1111-1111-1111-111111111111");
        private static readonly Guid Approver = Guid.Parse("22222222-2222-2222-2222-222222222222");
        private static readonly Guid Stranger = Guid.Parse("33333333-3333-3333-3333-333333333333");

        private const bool IsApprover = true;
        private const bool NotApprover = false;
        private const bool NoDuplicate = false;
        private const bool Duplicate = true;

        // ---- Exencion por profundidad (no bloquear al propio ResolveChangeRequestPlugin) ----

        [Theory]
        [InlineData(1, false)]
        [InlineData(2, true)]
        [InlineData(3, true)]
        public void IsWriteExempt_OnlyWhenDepthGreaterThanOne(int depth, bool expected)
        {
            Assert.Equal(expected, IsWriteExempt(depth));
        }

        [Fact]
        public void Exempt_SkipsAllRules_EvenWithParentSubmitted()
        {
            // El propio plugin de resolucion marca las lineas Applied/Denied con el RFC
            // ya en Submitted (no Draft): a profundidad > 1 el guard no debe opinar.
            var result = Evaluate(
                isExempt: true, parentResolved: true, parentStatus: Submitted,
                parentCreatorId: Creator, actorId: Stranger, isAuthorizedApprover: NotApprover,
                isDuplicateFieldDefinition: Duplicate);

            Assert.True(result.IsAllowed);
        }

        // ---- Regla: RFC padre en Draft ----

        [Fact]
        public void ParentInDraft_ByCreator_NoDuplicate_IsAllowed()
        {
            var result = Evaluate(
                isExempt: false, parentResolved: true, parentStatus: Draft,
                parentCreatorId: Creator, actorId: Creator, isAuthorizedApprover: NotApprover,
                isDuplicateFieldDefinition: NoDuplicate);

            Assert.True(result.IsAllowed);
        }

        [Theory]
        [InlineData(Submitted)]
        [InlineData(Approved)]
        [InlineData(Rejected)]
        [InlineData(Applied)]
        [InlineData(Failed)]
        [InlineData(Canceled)]
        public void ParentNotInDraft_IsBlocked(int parentStatus)
        {
            var result = Evaluate(
                isExempt: false, parentResolved: true, parentStatus: parentStatus,
                parentCreatorId: Creator, actorId: Creator, isAuthorizedApprover: NotApprover,
                isDuplicateFieldDefinition: NoDuplicate);

            Assert.False(result.IsAllowed);
            Assert.Contains("Draft", result.Reason);
        }

        [Fact]
        public void ParentNotResolved_IsBlockedFailClosed()
        {
            var result = Evaluate(
                isExempt: false, parentResolved: false, parentStatus: null,
                parentCreatorId: Creator, actorId: Creator, isAuthorizedApprover: NotApprover,
                isDuplicateFieldDefinition: NoDuplicate);

            Assert.False(result.IsAllowed);
        }

        // ---- Regla: creador del RFC o aprobador ----

        [Fact]
        public void ByApprover_NotCreator_IsAllowed()
        {
            var result = Evaluate(
                isExempt: false, parentResolved: true, parentStatus: Draft,
                parentCreatorId: Creator, actorId: Approver, isAuthorizedApprover: IsApprover,
                isDuplicateFieldDefinition: NoDuplicate);

            Assert.True(result.IsAllowed);
        }

        [Fact]
        public void ByStranger_NotCreatorNorApprover_IsBlocked()
        {
            var result = Evaluate(
                isExempt: false, parentResolved: true, parentStatus: Draft,
                parentCreatorId: Creator, actorId: Stranger, isAuthorizedApprover: NotApprover,
                isDuplicateFieldDefinition: NoDuplicate);

            Assert.False(result.IsAllowed);
        }

        [Fact]
        public void ParentCreatorUnknown_IsBlockedFailClosed()
        {
            var result = Evaluate(
                isExempt: false, parentResolved: true, parentStatus: Draft,
                parentCreatorId: Guid.Empty, actorId: Creator, isAuthorizedApprover: NotApprover,
                isDuplicateFieldDefinition: NoDuplicate);

            Assert.False(result.IsAllowed);
        }

        // ---- Regla: sin duplicados por (RFC, FieldDefinition) ----

        [Fact]
        public void DuplicateFieldDefinition_IsBlocked()
        {
            var result = Evaluate(
                isExempt: false, parentResolved: true, parentStatus: Draft,
                parentCreatorId: Creator, actorId: Creator, isAuthorizedApprover: NotApprover,
                isDuplicateFieldDefinition: Duplicate);

            Assert.False(result.IsAllowed);
            Assert.Contains("campo", result.Reason, StringComparison.OrdinalIgnoreCase);
        }
    }
}
