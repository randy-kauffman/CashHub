using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace CashHub.Validation.Reporting
{
    /// <summary>Un error tal como lo verá el cliente (ya renderizado y ubicado).</summary>
    public sealed class ReplyIssue
    {
        public ReplyIssue(string message, string? sheet = null, string? cellRef = null, int? row = null)
        {
            Message = message;
            Sheet = sheet;
            CellRef = cellRef;
            Row = row;
        }

        public string Message { get; }
        public string? Sheet { get; }
        public string? CellRef { get; }
        public int? Row { get; }

        /// <summary>Los errores del "sobre" no tienen ubicación en el archivo.</summary>
        public bool HasLocation => !string.IsNullOrEmpty(Sheet);
    }

    /// <summary>Totales por moneda de una bolsa declarada (para el acuse de recibo).</summary>
    public sealed class ReplyBagSummary
    {
        public ReplyBagSummary(string bagNumber, int deposits, decimal nio, decimal usd, decimal eur)
        {
            BagNumber = bagNumber;
            Deposits = deposits;
            Nio = nio;
            Usd = usd;
            Eur = eur;
        }

        public string BagNumber { get; }
        public int Deposits { get; }
        public decimal Nio { get; }
        public decimal Usd { get; }
        public decimal Eur { get; }
    }

    /// <summary>
    /// Arma el correo de respuesta al cliente. Reglas de diseño para email HTML:
    /// tablas para el layout (nada de flex/grid), estilos inline (Gmail y Outlook
    /// descartan &lt;style&gt;), sin imágenes externas (se bloquean por defecto),
    /// ancho fluido con tope de 640px y contraste alto para lectura en móvil.
    /// </summary>
    public static class ClientReplyBuilder
    {
        private const string Ink = "#242424";
        private const string Muted = "#616161";
        private const string Line = "#E1E1E1";
        private const string Canvas = "#F5F5F5";
        private const string OkDark = "#0E700E";
        private const string OkSoft = "#DFF6DD";
        private const string BadDark = "#A80000";
        private const string BadSoft = "#FDE7E9";
        private const string Font = "-apple-system,BlinkMacSystemFont,'Segoe UI',Roboto,Helvetica,Arial,sans-serif";

        public static string BuildAccepted(string clientName, string fileName,
            IReadOnlyList<ReplyBagSummary> bags, DateTime receivedOn,
            string? trackingId = null, string? runId = null)
        {
            var body = new StringBuilder();
            body.Append(Banner(true, "Declaracion recibida y validada",
                "Su declaracion paso todas las validaciones y ya esta registrada en el banco."));

            body.Append(Section("Resumen de lo declarado"));
            body.Append($@"<table role='presentation' width='100%' cellpadding='0' cellspacing='0' style='border-collapse:collapse;font-family:{Font};font-size:14px;color:{Ink};margin:0 0 24px'>
<tr style='background:{Canvas}'>
  <th align='left' style='padding:10px 12px;border-bottom:2px solid {Line};font-size:12px;letter-spacing:.4px;text-transform:uppercase;color:{Muted}'>Bolsa</th>
  <th align='center' style='padding:10px 12px;border-bottom:2px solid {Line};font-size:12px;letter-spacing:.4px;text-transform:uppercase;color:{Muted}'>Depositos</th>
  <th align='right' style='padding:10px 12px;border-bottom:2px solid {Line};font-size:12px;letter-spacing:.4px;text-transform:uppercase;color:{Muted}'>NIO</th>
  <th align='right' style='padding:10px 12px;border-bottom:2px solid {Line};font-size:12px;letter-spacing:.4px;text-transform:uppercase;color:{Muted}'>USD</th>
  <th align='right' style='padding:10px 12px;border-bottom:2px solid {Line};font-size:12px;letter-spacing:.4px;text-transform:uppercase;color:{Muted}'>EUR</th>
</tr>");
            foreach (var bag in bags)
            {
                body.Append($@"<tr>
  <td style='padding:12px;border-bottom:1px solid {Line};font-weight:600'>{Escape(bag.BagNumber)}</td>
  <td align='center' style='padding:12px;border-bottom:1px solid {Line}'>{bag.Deposits}</td>
  <td align='right' style='padding:12px;border-bottom:1px solid {Line}'>{Amount(bag.Nio)}</td>
  <td align='right' style='padding:12px;border-bottom:1px solid {Line}'>{Amount(bag.Usd)}</td>
  <td align='right' style='padding:12px;border-bottom:1px solid {Line}'>{Amount(bag.Eur)}</td>
</tr>");
            }
            if (bags.Count > 1)
            {
                body.Append($@"<tr style='background:{Canvas}'>
  <td style='padding:12px;font-weight:700'>Total</td>
  <td align='center' style='padding:12px;font-weight:700'>{bags.Sum(b => b.Deposits)}</td>
  <td align='right' style='padding:12px;font-weight:700'>{Amount(bags.Sum(b => b.Nio))}</td>
  <td align='right' style='padding:12px;font-weight:700'>{Amount(bags.Sum(b => b.Usd))}</td>
  <td align='right' style='padding:12px;font-weight:700'>{Amount(bags.Sum(b => b.Eur))}</td>
</tr>");
            }
            body.Append("</table>");

            body.Append(Note("Que sigue", "Entregue las bolsas selladas a la transportadora de valores junto con su planilla de conduccion. " +
                "El banco recontara el efectivo y acreditara segun lo declarado."));

            return Shell(clientName, fileName, receivedOn, body.ToString(), trackingId, runId);
        }

        public static string BuildRejected(string? clientName, string fileName,
            IReadOnlyList<ReplyIssue> issues, DateTime receivedOn,
            string? trackingId = null, string? runId = null)
        {
            var envelope = issues.Where(i => !i.HasLocation).ToList();
            var located = issues.Where(i => i.HasLocation).ToList();

            var body = new StringBuilder();
            var count = issues.Count;
            body.Append(Banner(false, count == 1 ? "Encontramos 1 punto por corregir" : $"Encontramos {count} puntos por corregir",
                "Su declaracion NO fue registrada. Corrija lo siguiente y responda a este correo con el archivo actualizado."));

            if (envelope.Count > 0)
            {
                body.Append(Section("Sobre el correo y el archivo"));
                body.Append($"<table role='presentation' width='100%' cellpadding='0' cellspacing='0' style='border-collapse:collapse;font-family:{Font};font-size:14px;color:{Ink};margin:0 0 24px'>");
                foreach (var issue in envelope)
                {
                    body.Append($@"<tr>
  <td width='28' valign='top' style='padding:12px 0 12px 12px;color:{BadDark};font-weight:700'>&bull;</td>
  <td style='padding:12px 12px 12px 4px;border-bottom:1px solid {Line}'>{Escape(issue.Message)}</td>
</tr>");
                }
                body.Append("</table>");
            }

            if (located.Count > 0)
            {
                body.Append(Section("En el archivo de Excel"));
                body.Append($@"<table role='presentation' width='100%' cellpadding='0' cellspacing='0' style='border-collapse:collapse;font-family:{Font};font-size:14px;color:{Ink};margin:0 0 8px'>
<tr style='background:{Canvas}'>
  <th align='left' style='padding:10px 12px;border-bottom:2px solid {Line};font-size:12px;letter-spacing:.4px;text-transform:uppercase;color:{Muted};white-space:nowrap'>Hoja / Celda</th>
  <th align='left' style='padding:10px 12px;border-bottom:2px solid {Line};font-size:12px;letter-spacing:.4px;text-transform:uppercase;color:{Muted}'>Que corregir</th>
</tr>");
                foreach (var group in located.GroupBy(i => i.Sheet))
                {
                    foreach (var issue in group.OrderBy(i => i.Row ?? 0))
                    {
                        var where = issue.CellRef != null
                            ? $"<span style='font-family:Consolas,Menlo,monospace;background:{Canvas};padding:2px 6px;border-radius:3px;white-space:nowrap'>{Escape(group.Key!)}!{Escape(issue.CellRef)}</span>"
                            : $"<span style='color:{Muted}'>{Escape(group.Key!)}{(issue.Row != null ? $" fila {issue.Row}" : "")}</span>";
                        body.Append($@"<tr>
  <td valign='top' style='padding:12px;border-bottom:1px solid {Line}'>{where}</td>
  <td style='padding:12px;border-bottom:1px solid {Line}'>{Escape(issue.Message)}</td>
</tr>");
                    }
                }
                body.Append("</table>");
                body.Append($"<p style='font-family:{Font};font-size:13px;color:{Muted};margin:0 0 24px'>Sugerencia: abra el archivo y presione Ctrl+I (o Ir a...) para saltar directo a cada celda.</p>");
            }

            body.Append(Note("Como continuar", "Corrija los puntos indicados en el mismo archivo y responda a este correo adjuntandolo. " +
                "La revision es automatica: recibira una confirmacion en pocos minutos."));

            return Shell(clientName, fileName, receivedOn, body.ToString(), trackingId, runId);
        }

        /// <summary>
        /// Respuesta para un contacto VIGENTE del banco que no está autorizado para el
        /// cliente que declara (vínculo inexistente, revocado o pendiente).
        ///
        /// No recibe clientName ni issues a propósito: no puede filtrar lo que no tiene.
        /// Confirmar que el CIF existe, nombrar al cliente o listar los hallazgos le
        /// revelaría a un tercero que ese cliente opera con el banco — exactamente la fuga
        /// que la puerta de respuesta existe para evitar.
        /// </summary>
        public static string BuildGeneric(string fileName, DateTime receivedOn,
            string? trackingId = null, string? runId = null)
        {
            var body = new StringBuilder();
            body.Append(Banner(false, "No pudimos procesar esta declaracion",
                "Su declaracion NO fue registrada."));
            body.Append(Note("Como continuar",
                "Los datos indicados en el archivo no corresponden a una cuenta que podamos procesar " +
                "desde esta direccion de correo. Contacte a su ejecutivo de Banca de Empresas para " +
                "revisar el envio."));

            return Shell(null, fileName, receivedOn, body.ToString(), trackingId, runId);
        }

        // ---------- piezas compartidas ----------

        private static string Shell(string? clientName, string fileName, DateTime receivedOn, string inner,
            string? trackingId = null, string? runId = null) =>
            $@"<!DOCTYPE html><html><body style='margin:0;padding:0;background:{Canvas}'>
<table role='presentation' width='100%' cellpadding='0' cellspacing='0' style='background:{Canvas};padding:24px 12px'>
<tr><td align='center'>
<table role='presentation' width='640' cellpadding='0' cellspacing='0' style='max-width:640px;width:100%;background:#FFFFFF;border:1px solid {Line};border-radius:8px;overflow:hidden'>
  <tr><td style='padding:24px 24px 0'>
    <p style='margin:0;font-family:{Font};font-size:13px;letter-spacing:1.5px;text-transform:uppercase;color:{Muted};font-weight:700'>CashHub</p>
    <p style='margin:2px 0 0;font-family:{Font};font-size:13px;color:{Muted}'>Pre-registro de depositos</p>
  </td></tr>
  <tr><td style='padding:16px 24px 24px'>{inner}
    <table role='presentation' width='100%' cellpadding='0' cellspacing='0' style='border-top:1px solid {Line};margin-top:8px'>
      <tr><td style='padding-top:16px;font-family:{Font};font-size:12px;color:{Muted};line-height:1.7'>
        {(clientName != null ? $"Cliente: <strong style='color:{Ink}'>{Escape(clientName)}</strong><br>" : "")}
        Archivo: {Escape(fileName)}<br>
        Recibido: {receivedOn:dd/MM/yyyy HH:mm} (hora del banco)
      </td></tr>
      {(trackingId != null ? $@"<tr><td style='padding-top:14px'>
        <table role='presentation' cellpadding='0' cellspacing='0' style='background:{Canvas};border-radius:6px'>
          <tr><td style='padding:12px 14px;font-family:{Font};font-size:12px;color:{Muted};line-height:1.7'>
            <strong style='color:{Ink}'>Numero de gestion:</strong>
            <span style='font-family:Consolas,Menlo,monospace;font-size:13px;color:{Ink};font-weight:700'>{Escape(trackingId)}</span>
            {(runId != null ? $"<br><span style='font-size:11px'>Validacion: <span style='font-family:Consolas,Menlo,monospace'>{Escape(runId)}</span></span>" : "")}
            <br><span style='font-size:11px'>Cite este numero para cualquier consulta sobre esta declaracion.</span>
          </td></tr>
        </table>
      </td></tr>" : "")}
      <tr><td style='padding-top:16px;font-family:{Font};font-size:11px;color:{Muted}'>
        Este es un mensaje automatico. Si necesita ayuda, responda a este correo.
      </td></tr>
    </table>
  </td></tr>
</table>
</td></tr></table></body></html>";

        private static string Banner(bool ok, string title, string subtitle) =>
            $@"<table role='presentation' width='100%' cellpadding='0' cellspacing='0' style='border-collapse:collapse;margin:0 0 24px'>
<tr><td style='background:{(ok ? OkSoft : BadSoft)};border-left:4px solid {(ok ? OkDark : BadDark)};border-radius:4px;padding:16px 18px'>
  <p style='margin:0;font-family:{Font};font-size:17px;font-weight:700;color:{(ok ? OkDark : BadDark)}'>{(ok ? "&#10003;" : "&#33;")} {Escape(title)}</p>
  <p style='margin:6px 0 0;font-family:{Font};font-size:14px;line-height:1.6;color:{Ink}'>{Escape(subtitle)}</p>
</td></tr></table>";

        private static string Section(string title) =>
            $"<p style='margin:0 0 10px;font-family:{Font};font-size:13px;font-weight:700;letter-spacing:.4px;text-transform:uppercase;color:{Muted}'>{Escape(title)}</p>";

        private static string Note(string title, string text) =>
            $@"<table role='presentation' width='100%' cellpadding='0' cellspacing='0' style='background:{Canvas};border-radius:6px;margin:0 0 8px'>
<tr><td style='padding:16px 18px;font-family:{Font};font-size:14px;line-height:1.6;color:{Ink}'>
  <strong>{Escape(title)}</strong><br>{Escape(text)}
</td></tr></table>";

        private static string Amount(decimal value) =>
            value == 0 ? $"<span style='color:{Muted}'>&mdash;</span>" : value.ToString("N2", System.Globalization.CultureInfo.InvariantCulture);

        private static string Escape(string? text) =>
            (text ?? string.Empty).Replace("&", "&amp;").Replace("<", "&lt;").Replace(">", "&gt;").Replace("\"", "&quot;");
    }
}
