

namespace KyrolusSous.Validation.Runtime.UnitTests;

public class ServiceCollectionExtensionsTests
{
    #region AddKyrolusValidationRuntime
    [Fact(DisplayName = "AddKyrolusValidationRuntime adds all required services to the service collection")]
    public void AddKyrolusValidationRuntime_AddsAllRequiredServices()
    {
        // Arrange
        var services = new ServiceCollection();

        // Act
        services.AddKyrolusValidationRuntime();
        var serviceProvider = services.BuildServiceProvider();
        // Assert
        TestHelper.AddsAllRequiredServices(serviceProvider);
    }
    #endregion
    #region AddKyrolusValidationProfile
    [Fact(DisplayName = "AddKyrolusValidationProfile adds a single profile to the service collection and all required services")]
    public void AddKyrolusValidationProfile_AddsSingleProfile()
    {
        // Arrange
        var services = new ServiceCollection();
        var profile = new KyrolusValidationProfile("TestProfile", new KyrolusValidationContext());

        // Act
        services.AddKyrolusValidationProfile(profile);
        var serviceProvider = services.BuildServiceProvider();

        // Assert
        var registeredProfile = serviceProvider.GetService<KyrolusValidationProfile>();
        registeredProfile.ShouldNotBeNull();
        registeredProfile.ShouldBe(profile);

        TestHelper.AddsAllRequiredServices(serviceProvider);
    }

    [Fact(DisplayName = "AddKyrolusValidationProfile throws ArgumentNullException when profile is null")]
    public void AddKyrolusValidationProfile_ThrowsArgumentNullException_WhenProfileIsNull()
    {
        var services = new ServiceCollection();
        Should.Throw<ArgumentNullException>(() => services.AddKyrolusValidationProfile(null!));
    }
    #endregion

    #region AddKyrolusValidationProfiles
    [Fact(DisplayName = "AddKyrolusValidationProfiles adds multiple profiles to the service collection and all required services")]
    public void AddKyrolusValidationProfiles_AddsMultipleProfiles()
    {
        // Arrange
        var services = new ServiceCollection();
        var profile1 = new KyrolusValidationProfile("TestProfile1", new KyrolusValidationContext());
        var profile2 = new KyrolusValidationProfile("TestProfile2", new KyrolusValidationContext());

        // Act
        services.AddKyrolusValidationProfiles(profile1, profile2);
        var serviceProvider = services.BuildServiceProvider();

        // Assert
        var registeredProfiles = serviceProvider.GetServices<KyrolusValidationProfile>().ToList();
        registeredProfiles.ShouldNotBeNull();
        registeredProfiles.ShouldContain(profile1);
        registeredProfiles.ShouldContain(profile2);

        TestHelper.AddsAllRequiredServices(serviceProvider);
    }

    [Fact(DisplayName = "AddKyrolusValidationProfiles throws ArgumentNullException when profiles is null")]
    public void AddKyrolusValidationProfiles_ThrowsArgumentNullException_WhenProfilesIsNull()
    {
        var services = new ServiceCollection();
        Should.Throw<ArgumentNullException>(() => services.AddKyrolusValidationProfiles(null!));
    }

    [Fact(DisplayName = "AddKyrolusValidationProfiles throws ArgumentException when profiles is empty")]
    public void AddKyrolusValidationProfiles_ThrowsArgumentException_WhenProfilesIsEmpty()
    {
        var services = new ServiceCollection();
        var ex = Should.Throw<ArgumentException>(() => services.AddKyrolusValidationProfiles([]));
        ex.ParamName.ShouldBe("profiles");
        ex.Message.ShouldContain("At least one profile must be provided.");
    }
    #endregion

    #region AddKyrolusValidationRuntimeScanning
    [Fact(DisplayName = "AddKyrolusValidationRuntimeScanning adds all required services and registers validators from the specified assemblies")]
    public void AddKyrolusValidationRuntimeScanning_AddsAllRequiredServicesAndRegistersValidators()
    {
        // Arrange
        var services = new ServiceCollection();
        var assembly = typeof(ServiceCollectionExtensionsTests).Assembly;

        // Act
        services.AddKyrolusValidationRuntimeScanning(assembly);
        var serviceProvider = services.BuildServiceProvider();

        // Assert
        TestHelper.AddsAllRequiredServices(serviceProvider);

        // Check that validators from the specified assembly are registered
        var validatorTypes = assembly.GetTypes().Where(t => t.GetInterfaces().Any(i => i.IsGenericType && i.GetGenericTypeDefinition() == typeof(IKyrolusRequestValidator<>)));
        foreach (var validatorType in validatorTypes)
        {
            var validatorInterface = validatorType.GetInterfaces().First(i => i.IsGenericType && i.GetGenericTypeDefinition() == typeof(IKyrolusRequestValidator<>));
            var registeredValidators = serviceProvider.GetServices(validatorInterface);
            registeredValidators.ShouldNotBeNull();
            registeredValidators.ShouldContain(v => v != null && v.GetType() == validatorType);
        }
    }
    #endregion
}
