namespace KyrolusSous.Validation.Runtime.UnitTests;

public class KyrolusValidationAbstractionsTests
{
    #region KyrolusValidationGroup
    [Fact(DisplayName = "KyrolusValidationGroup should store name property correctly")]
    public void KyrolusValidationGroup_ShouldStoreName()
    {
        var group = new KyrolusValidationGroup("UiHints");
        group.Names.ShouldContain("UiHints");
    }
    #endregion

    #region KyrolusValidationException
    [Fact(DisplayName = "KyrolusValidationException should format rich message and store errors list")]
    public void KyrolusValidationException_ShouldStoreErrorsAndMessage()
    {
        IReadOnlyList<KyrolusValidationFailure> errors = [
            new KyrolusValidationFailure("Email", "Email is required"),
            new KyrolusValidationFailure("Age", "Must be at least 18")
        ];
        var exception = new KyrolusValidationException(errors);

        exception.Message.ShouldBe("Validation failed for 2 rule(s): Email: Email is required; Age: Must be at least 18");
        exception.Errors.ShouldBe(errors);
        exception.Errors.Count.ShouldBe(2);

        var customMsgEx = new KyrolusValidationException("Custom failure message", errors);
        customMsgEx.Message.ShouldBe("Custom failure message");

        var emptyEx = new KyrolusValidationException([]);
        emptyEx.Message.ShouldBe("Validation failed.");
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
