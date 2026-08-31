using System;
using CashHub.Validation.Security;
using Xunit;
using static CashHub.Validation.Security.AppCatalogGuard;

namespace CashHub.Validation.Tests
{
    /// <summary>
    /// Especificacion del guard del catalogo de apps del hub (CH-Tbl-AppCatalogs).
    /// El agujero que cierra: el hub lanza sanic_appurl tal cual (Launch), asi que una URL
    /// arbitraria escrita por Web API/Excel convierte al hub en un lanzador de phishing con
    /// identidad corporativa. Ademas, un sanic_aadgroupid malformado rompe el gating por
    /// grupos en silencio (el GUID nunca matchea y la app desaparece para todos).
    /// </summary>
    public class AppCatalogGuardTests
    {
        private const string G1 = "f90dd033-1f5f-4b76-983d-b0dc6a9ede94";
        private const string G2 = "17b5e5c7-e7a0-4b61-98cd-ceff67211797";

        // ---- sanic_appurl ----

        [Fact]
        public void AppUrl_PowerAppsPlayerUrl_IsAllowed()
        {
            var result = EvaluateAppUrl("https://apps.powerapps.com/play/e/default-abc/a/12345678-aaaa-bbbb-cccc-000000000000?tenantId=x");
            Assert.True(result.IsAllowed);
        }

        [Fact]
        public void AppUrl_Blank_IsAllowed()
        {
            // Fila en preparacion (Enabled=false): sin URL no hay nada que lanzar ni que validar.
            Assert.True(EvaluateAppUrl(null).IsAllowed);
            Assert.True(EvaluateAppUrl("   ").IsAllowed);
        }

        [Fact]
        public void AppUrl_WithSurroundingWhitespace_IsAllowed()
        {
            var result = EvaluateAppUrl("  https://apps.powerapps.com/play/e/x/a/y  ");
            Assert.True(result.IsAllowed);
        }

        [Fact]
        public void AppUrl_Http_IsBlocked()
        {
            var result = EvaluateAppUrl("http://apps.powerapps.com/play/e/x/a/y");
            Assert.False(result.IsAllowed);
            Assert.Contains("https", result.Reason);
        }

        [Fact]
        public void AppUrl_OtherHost_IsBlocked()
        {
            var result = EvaluateAppUrl("https://evil.example.com/play/e/x/a/y");
            Assert.False(result.IsAllowed);
            Assert.Contains(AllowedHost, result.Reason);
        }

        [Fact]
        public void AppUrl_HostSuffixTrick_IsBlocked()
        {
            // apps.powerapps.com.evil.com NO es apps.powerapps.com.
            var result = EvaluateAppUrl("https://apps.powerapps.com.evil.com/play");
            Assert.False(result.IsAllowed);
        }

        [Fact]
        public void AppUrl_UserInfoTrick_IsBlocked()
        {
            // https://apps.powerapps.com@evil.com/ -> el host real es evil.com.
            var result = EvaluateAppUrl("https://apps.powerapps.com@evil.com/play");
            Assert.False(result.IsAllowed);
        }

        [Fact]
        public void AppUrl_NonDefaultPort_IsBlocked()
        {
            var result = EvaluateAppUrl("https://apps.powerapps.com:8443/play");
            Assert.False(result.IsAllowed);
        }

        [Fact]
        public void AppUrl_NotAUrl_IsBlocked()
        {
            Assert.False(EvaluateAppUrl("javascript:alert(1)").IsAllowed);
            Assert.False(EvaluateAppUrl("esto no es una url").IsAllowed);
        }

        // ---- tabla de accesos: sanic_aadgroupid = UN grupo AAD por fila ----

        [Fact]
        public void AccessGroup_SingleGuid_IsAllowed()
        {
            var result = EvaluateAccessGroupId(G1, out var parsed);
            Assert.True(result.IsAllowed);
            Assert.Equal(Guid.Parse(G1), parsed);
        }

        [Fact]
        public void AccessGroup_GuidWithSpaces_IsAllowed()
        {
            var result = EvaluateAccessGroupId($"  {G1}  ", out var parsed);
            Assert.True(result.IsAllowed);
            Assert.Equal(Guid.Parse(G1), parsed);
        }

        [Fact]
        public void AccessGroup_Blank_IsBlocked()
        {
            // La fila SOLO existe para declarar un grupo: sin grupo no tiene sentido.
            Assert.False(EvaluateAccessGroupId(null, out _).IsAllowed);
            Assert.False(EvaluateAccessGroupId("   ", out _).IsAllowed);
        }

        [Fact]
        public void AccessGroup_MultipleGuids_IsBlocked()
        {
            // El ';' murio con el modelo viejo: un grupo por fila.
            var result = EvaluateAccessGroupId($"{G1};{G2}", out _);
            Assert.False(result.IsAllowed);
            Assert.Contains("una fila por grupo", result.Reason);
        }

        [Fact]
        public void AccessGroup_NotAGuid_IsBlocked()
        {
            var result = EvaluateAccessGroupId("no-soy-un-guid", out _);
            Assert.False(result.IsAllowed);
            Assert.Contains("no-soy-un-guid", result.Reason);
        }
    }
}
