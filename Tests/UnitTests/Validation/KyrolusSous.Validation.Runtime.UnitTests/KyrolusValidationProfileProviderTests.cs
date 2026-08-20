using System.Reflection;

namespace KyrolusSous.Validation.Runtime.UnitTests;

public class KyrolusValidationProfileProviderTests
{
    [Theory(DisplayName = "KyrolusValidationProfileProvider should ignore the profile if it is null or its name is null, empty space or whitespace")]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("    ")]
    public void KyrolusValidationProfileProvider_Ignore_null_profileName_Is_null_whitespace_emptyspace(string? profileName)
    {
        var profiles = new List<KyrolusValidationProfile>()
        {
            new(profileName!, KyrolusValidationContext.Default), null!,
            new("prof1", KyrolusValidationContext.Default)
        };
        var provider = new KyrolusValidationProfileProvider(profiles);
        var fieldInfo = typeof(KyrolusValidationProfileProvider).GetField("profiles", BindingFlags.NonPublic | BindingFlags.Instance);

        var profilesInProviders = fieldInfo?.GetValue(provider) as Dictionary<string, KyrolusValidationContext>;

        profilesInProviders?.Count.ShouldBe(1);
    }
    [Theory(DisplayName = "KyrolusValidationProfileProvider should return the default context when the profile name is null, empty string or white space")]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("    ")]

    public void KyrolusValidationProfileProvider_DefaultContext_profileName_Is_null_whitespace_emptyspace(string? profileName)
    {
        var profiles = new List<KyrolusValidationProfile>()
        {
            new(profileName!, KyrolusValidationContext.Default), null!,
            new("prof1", KyrolusValidationContext.Default)
        };
        var provider = new KyrolusValidationProfileProvider(profiles);
        var profile = provider.TryGetProfile(profileName!, out var context);
        context.ShouldBe(KyrolusValidationContext.Default);
        profile.ShouldBeFalse();
    }
    [Fact(DisplayName = "KyrolusValidationProfileProvider should return the context if the profile is found")]
    public void KyrolusValidationProfileProvider_Return_CorrectContext_If_ProfileIsFound()
    {
        var contextToTest = new KyrolusValidationContext(["Rule1"]);
        var profiles = new List<KyrolusValidationProfile>()
        {
            new("prof1",contextToTest)
        };
        var provider = new KyrolusValidationProfileProvider(profiles);
        var profile = provider.TryGetProfile("prof1", out var contextFound);
        contextFound.ShouldBe(contextToTest);
        profile.ShouldBeTrue();
    }
    [Fact(DisplayName = "KyrolusValidationProfileProvider should return the default context and false if the profile is not found")]
    public void KyrolusValidationProfileProvider_Return_DefaultContext_False_ProfileNotFound()
    {
        var contextToTest = new KyrolusValidationContext(["Rule1"]);
        var profiles = new List<KyrolusValidationProfile>()
        {
            new("prof1",contextToTest)
        };
        var provider = new KyrolusValidationProfileProvider(profiles);
        var profile = provider.TryGetProfile("prof2", out var contextFound);
        contextFound.ShouldBe(KyrolusValidationContext.Default);
        profile.ShouldBeFalse();
    }
}
