namespace KyrolusSous.Mapping.UnitTests;

public sealed class CustomConverterAndResolverTests
{
    private sealed record Money(decimal Amount, string Currency);

    private sealed class MoneyToDecimalConverter : IKyrolusTypeConverter<Money, decimal>
    {
        public decimal Convert(Money source, KyrolusMappingContext context) => source.Amount;
    }

    private sealed class Account
    {
        public int Id { get; set; }
        public Money Balance { get; set; } = new(0, "USD");
    }

    private sealed class AccountDto
    {
        public int Id { get; set; }
        public decimal Balance { get; set; }
    }

    [Fact(DisplayName = "IKyrolusTypeConverter: Custom type converter transforms entire type")]
    public void CustomConverter_TransformsType()
    {
        var config = new KyrolusMappingConfiguration();
        config.CreateMap<Money, decimal>()
            .ConvertUsing(src => src.Amount);

        var mapper = new KyrolusObjectMapper(config);
        var account = new Account { Id = 10, Balance = new Money(1500.75m, "USD") };

        var dto = mapper.Map<Account, AccountDto>(account);

        dto.ShouldNotBeNull();
        dto.Id.ShouldBe(10);
        dto.Balance.ShouldBe(1500.75m);
    }
}
