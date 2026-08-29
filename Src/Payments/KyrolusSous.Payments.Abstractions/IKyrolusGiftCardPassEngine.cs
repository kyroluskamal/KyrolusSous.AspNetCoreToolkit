namespace KyrolusSous.Payments.Abstractions;

public interface IKyrolusGiftCardPassEngine
{
    KyrolusGiftCard IssueGiftCard(KyrolusIssueGiftCardRequest request);
    KyrolusRedeemGiftCardResult RedeemGiftCard(string cardCode, string pin, decimal amountToDeduct);
    decimal GetBalance(string cardCode, string pin);
}
