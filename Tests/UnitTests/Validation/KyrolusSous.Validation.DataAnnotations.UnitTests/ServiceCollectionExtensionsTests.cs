using Microsoft.Extensions.DependencyInjection;

namespace KyrolusSous.Validation.DataAnnotations.UnitTests;

public class ServiceCollectionExtensionsTests
{
    [Fact(DisplayName = "AddDataAnnotationsRequestValidator should register DataAnnotationsRequestValidator in the service collection")]
    public void AddDataAnnotationsRequestValidator_RegistersValidatorInServiceCollection()
    {
        // Arrange
        var services = new ServiceCollection();

        // Act
        services.AddKyrolusDataAnnotationsValidation();

        // Assert
        var serviceProvider = services.BuildServiceProvider();
        var validator = serviceProvider.GetService<IKyrolusRequestValidator<TestUserRequest>>();
        validator.ShouldNotBeNull();
        validator.ShouldBeOfType<DataAnnotationsRequestValidator<TestUserRequest>>();
    }
}
