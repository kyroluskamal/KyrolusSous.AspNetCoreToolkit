using System.Net;
using System.Text;
using KyrolusSous.Payments.Abstractions;

namespace KyrolusSous.Payments.Core;

public sealed class KyrolusDefaultInvoiceGenerator : IKyrolusInvoiceGenerator
{
    public KyrolusInvoiceResult GenerateInvoice(KyrolusInvoiceRequest request)
    {
        decimal subtotal = 0m;
        decimal totalTax = 0m;

        foreach (var item in request.Items)
        {
            var lineSubtotal = item.UnitPrice * item.Quantity;
            var lineTax = lineSubtotal * (item.TaxRatePercent / 100m);
            subtotal += lineSubtotal;
            totalTax += lineTax;
        }

        var total = (subtotal + totalTax) - request.DiscountAmount;

        var sb = new StringBuilder();
        sb.AppendLine("<!DOCTYPE html><html><head><meta charset='utf-8'><title>Invoice</title>");
        sb.AppendLine("<style>body{font-family:sans-serif;padding:20px;} table{width:100%;border-collapse:collapse;} th,td{border:1px solid #ddd;padding:8px;text-align:left;} th{background:#f4f4f4;}</style>");
        sb.AppendLine("</head><body>");
        sb.AppendLine($"<h2>INVOICE #{WebUtility.HtmlEncode(request.InvoiceNumber)}</h2>");
        sb.AppendLine($"<p><strong>Merchant:</strong> {WebUtility.HtmlEncode(request.MerchantName)} (Tax ID: {WebUtility.HtmlEncode(request.MerchantTaxNumber ?? "N/A")})</p>");
        sb.AppendLine($"<p><strong>Customer:</strong> {WebUtility.HtmlEncode(request.CustomerName)} ({WebUtility.HtmlEncode(request.CustomerEmail ?? "N/A")})</p>");
        sb.AppendLine($"<p><strong>Date:</strong> {request.IssueDateUtc:yyyy-MM-dd}</p>");
        sb.AppendLine("<table><thead><tr><th>Description</th><th>Qty</th><th>Unit Price</th><th>Tax %</th><th>Total</th></tr></thead><tbody>");

        foreach (var item in request.Items)
        {
            sb.AppendLine($"<tr><td>{WebUtility.HtmlEncode(item.Description)}</td><td>{item.Quantity}</td><td>{item.UnitPrice:F2}</td><td>{item.TaxRatePercent}%</td><td>{item.TotalAmount:F2}</td></tr>");
        }

        sb.AppendLine("</tbody></table>");
        sb.AppendLine($"<p><strong>Subtotal:</strong> {subtotal:F2} {WebUtility.HtmlEncode(request.Currency)}</p>");
        sb.AppendLine($"<p><strong>Tax:</strong> {totalTax:F2} {WebUtility.HtmlEncode(request.Currency)}</p>");
        if (request.DiscountAmount > 0)
        {
            sb.AppendLine($"<p><strong>Discount:</strong> -{request.DiscountAmount:F2} {WebUtility.HtmlEncode(request.Currency)}</p>");
        }
        sb.AppendLine($"<h3>Total Due: {total:F2} {WebUtility.HtmlEncode(request.Currency)}</h3>");
        if (!string.IsNullOrEmpty(request.Notes))
        {
            sb.AppendLine($"<p><em>Notes: {WebUtility.HtmlEncode(request.Notes)}</em></p>");
        }
        sb.AppendLine("</body></html>");

        return new KyrolusInvoiceResult
        {
            InvoiceNumber = request.InvoiceNumber,
            SubtotalAmount = subtotal,
            TaxAmount = totalTax,
            DiscountAmount = request.DiscountAmount,
            TotalAmount = total,
            Currency = request.Currency,
            RenderedHtml = sb.ToString()
        };
    }
}
