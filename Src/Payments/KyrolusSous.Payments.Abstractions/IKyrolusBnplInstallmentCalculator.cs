namespace KyrolusSous.Payments.Abstractions;

public interface IKyrolusBnplInstallmentCalculator
{
    KyrolusBnplCalculationResult CalculatePlans(decimal orderAmount, string currency = "EGP");
}
