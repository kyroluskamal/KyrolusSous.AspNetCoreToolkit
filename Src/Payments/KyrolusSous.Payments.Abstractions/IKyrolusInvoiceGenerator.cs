namespace KyrolusSous.Payments.Abstractions;

public interface IKyrolusInvoiceGenerator
{
    KyrolusInvoiceResult GenerateInvoice(KyrolusInvoiceRequest request);
}
