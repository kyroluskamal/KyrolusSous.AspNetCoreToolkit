namespace KyrolusSous.Payments.Abstractions;

public enum KyrolusPaymentStatus
{
    Pending,
    RequiresAction,
    Processing,
    Succeeded,
    Failed,
    Cancelled,
    Refunded,
    PartiallyRefunded
}

public enum KyrolusPaymentMethodType
{
    CreditCard,
    DebitCard,
    DigitalWallet,
    BankTransfer,
    DirectDebit,
    BuyNowPayLater,
    KioskOrRetail,
    InstaPay,
    Other
}
