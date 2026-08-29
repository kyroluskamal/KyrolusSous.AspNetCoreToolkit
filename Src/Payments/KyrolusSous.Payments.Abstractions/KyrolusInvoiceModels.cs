namespace KyrolusSous.Payments.Abstractions;

public sealed record KyrolusInvoiceItem
{
    public required string Description { get; init; }
    public decimal UnitPrice { get; init; }
    public int Quantity { get; init; } = 1;
    public decimal TaxRatePercent { get; init; } = 0m;
    public decimal TotalAmount => (UnitPrice * Quantity) * (1 + (TaxRatePercent / 100m));
}

public sealed record KyrolusInvoiceRequest
{
    public required string InvoiceNumber { get; init; }
    public required string MerchantName { get; init; }
    public string? MerchantTaxNumber { get; init; }
    public required string CustomerName { get; init; }
    public string? CustomerEmail { get; init; }
    public string? CustomerAddress { get; init; }
    public required string Currency { get; init; }
    public IReadOnlyList<KyrolusInvoiceItem> Items { get; init; } = [];
    public decimal DiscountAmount { get; init; } = 0m;
    public DateTimeOffset IssueDateUtc { get; init; } = DateTimeOffset.UtcNow;
    public DateTimeOffset? DueDateUtc { get; init; }
    public string? Notes { get; init; }
}

public sealed record KyrolusInvoiceResult
{
    public required string InvoiceNumber { get; init; }
    public decimal SubtotalAmount { get; init; }
    public decimal TaxAmount { get; init; }
    public decimal DiscountAmount { get; init; }
    public decimal TotalAmount { get; init; }
    public string Currency { get; init; } = "USD";
    public string RenderedHtml { get; init; } = string.Empty;
}
