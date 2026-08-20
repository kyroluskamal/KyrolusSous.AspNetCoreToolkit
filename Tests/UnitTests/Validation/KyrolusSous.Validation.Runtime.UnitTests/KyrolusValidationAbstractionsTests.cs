namespace KyrolusSous.Validation.Runtime.UnitTests;

public class KyrolusValidationAbstractionsTests
{
    #region KyrolusValidationGroup
    [Fact(DisplayName = "KyrolusValidationGroup should store name property correctly")]
    public void KyrolusValidationGroup_ShouldStoreName()
    {
        var group = new KyrolusValidationGroup("UiHints");
        group.Name.ShouldBe("UiHints");
    }
    #endregion

    #region KyrolusValidationException
    [Fact(DisplayName = "KyrolusValidationException should store errors list and default message")]
    public void KyrolusValidationException_ShouldStoreErrorsAndMessage()
    {
        IReadOnlyList<KyrolusValidationFailure> errors = [new KyrolusValidationFailure("Prop", "Error")];
        var exception = new KyrolusValidationException(errors);

        exception.Message.ShouldBe("Validation failed.");
        exception.Errors.ShouldBe(errors);
        exception.Errors.Count.ShouldBe(1);
    }
    #endregion

    #region KyrolusValidationProfiles
    [Fact(DisplayName = "KyrolusValidationProfiles static properties should be configured correctly")]
    public void KyrolusValidationProfiles_StaticProperties_ShouldBeConfiguredCorrectly()
    {
        var createProfile = KyrolusValidationProfiles.Create;
        createProfile.Name.ShouldBe("Create");
        createProfile.Context.RuleSets!.ShouldContain("Create");

        var updateProfile = KyrolusValidationProfiles.Update;
        updateProfile.Name.ShouldBe("Update");
        updateProfile.Context.RuleSets!.ShouldContain("Update");

        var uiHintsProfile = KyrolusValidationProfiles.UiHints;
        uiHintsProfile.Name.ShouldBe("UiHints");
        uiHintsProfile.Context.Groups!.ShouldContain("UiHints");

        var backgroundProfile = KyrolusValidationProfiles.BackgroundJobs;
        backgroundProfile.Name.ShouldBe("BackgroundJobs");
        backgroundProfile.Context.RuleSets!.ShouldContain("BackgroundJobs");

        var allProfiles = KyrolusValidationProfiles.All;
        allProfiles.Count.ShouldBe(4);
        allProfiles.ShouldContain(createProfile);
        allProfiles.ShouldContain(updateProfile);
        allProfiles.ShouldContain(uiHintsProfile);
        allProfiles.ShouldContain(backgroundProfile);
    }
    #endregion
}
