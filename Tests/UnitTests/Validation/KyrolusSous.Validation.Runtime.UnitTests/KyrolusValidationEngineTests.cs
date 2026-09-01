namespace KyrolusSous.Validation.Runtime.UnitTests;

public class KyrolusValidationEngineTests
{
    [Fact(DisplayName = "KyrolusValidationEngine throws ArgumentNullException when profileProvider is null")]
    public void KyrolusValidationEngine_ThrowsArgumentNullException_WhenProfileProviderIsNull()
    {
        Should.Throw<ArgumentNullException>(() => new KyrolusValidationEngine(null!));
    }

    #region ValidateAsync
    [Fact(DisplayName = "ValidateAsync returns validation failures for invalid request")]
    public async Task ValidateAsync_ReturnsValidationFailures_ForInvalidRequest()
    {
        // Arrange
        var serviceProvider = TestHelper.BuildServiceProviderWithValidationRuntime(services =>
            services.AddSingleton<IKyrolusRequestValidator<TestRequest>, TestValidator>()
        );
        var validationEngine = serviceProvider.GetRequiredService<IKyrolusValidationEngine>();

        var request = new TestRequest { Name = "", Age = -1 };
        var context = new KyrolusValidationContext();

        // Act
        var failures = await validationEngine.ValidateAsync(request, context);

        // Assert
        failures.ShouldNotBeNull();
        failures.ShouldContain(f => f.PropertyName == nameof(request.Name) && f.ErrorMessage == "Name cannot be null or whitespace.");
        failures.ShouldContain(f => f.PropertyName == nameof(request.Age) && f.ErrorMessage == "Age cannot be negative.");
    }

    [Fact(DisplayName = "ValidateAsync returns empty list for valid request")]
    public async Task ValidateAsync_ReturnsEmptyList_ForValidRequest()
    {
        // Arrange
        var serviceProvider = TestHelper.BuildServiceProviderWithValidationRuntime(services =>
            services.AddSingleton<IKyrolusRequestValidator<TestRequest>, TestValidator>()
        );
        var validationEngine = serviceProvider.GetRequiredService<IKyrolusValidationEngine>();

        var request = new TestRequest { Name = "Valid Name", Age = 25 };
        var context = new KyrolusValidationContext();

        // Act
        var failures = await validationEngine.ValidateAsync(request, context);

        // Assert
        failures.ShouldNotBeNull();
        failures.ShouldBeEmpty();
    }

    [Fact(DisplayName = "ValidateAsync returns empty list when no validator is registered for the request type")]
    public async Task ValidateAsync_ReturnsEmptyList_WhenNoValidatorRegistered()
    {
        // Arrange
        var serviceProvider = TestHelper.BuildServiceProviderWithValidationRuntime();
        var validationEngine = serviceProvider.GetRequiredService<IKyrolusValidationEngine>();

        var request = new TestRequest { Name = "Valid Name", Age = 25 };
        var context = new KyrolusValidationContext();

        // Act
        var failures = await validationEngine.ValidateAsync(request, context);

        // Assert
        failures.ShouldNotBeNull();
        failures.ShouldBeEmpty();
    }

    [Fact(DisplayName = "ValidateAsync can be called with default context")]
    public async Task ValidateAsync_CanBeCalledWIthDefaultContext()
    {
        var serviceProvider = TestHelper.BuildServiceProviderWithValidationRuntime();
        var validationEngine = serviceProvider.GetRequiredService<IKyrolusValidationEngine>();

        var request = new TestRequest { Name = "Valid Name", Age = 25 };
        // Act
        var failures = await validationEngine.ValidateAsync(request);

        // Assert
        failures.ShouldNotBeNull();
        failures.ShouldBeEmpty();
    }

    [Fact(DisplayName = "ValidateAsync should apply profiles and merge rule sets and minimum severity with context")]
    public async Task ValidateAsync_ShouldApplyProfilesAndMergeItWithTheContext()
    {
        // Arrange
        // 1. Profile1 defines RuleSetA and Warning severity
        var profile1 = new KyrolusValidationProfile(
            "Profile1", new KyrolusValidationContext(RuleSets: ["RuleSetA"], MinimumSeverity: KyrolusValidationSeverity.Warning)
        );

        // 2. Profile2 defines RuleSetB and Error severity
        var profile2 = new KyrolusValidationProfile(
            "Profile2",new KyrolusValidationContext(RuleSets: ["RuleSetB"], MinimumSeverity: KyrolusValidationSeverity.Error)
        );

        var serviceProvider = TestHelper.BuildServiceProviderWithValidationRuntime(services =>
        {
            services.AddKyrolusValidationProfiles(profile1, profile2);
            services.AddSingleton<IKyrolusRequestValidator<ProfileTestRequest>, ProfileTestValidator>();
        });

        var validationEngine = serviceProvider.GetRequiredService<IKyrolusValidationEngine>();

        var request = new ProfileTestRequest { Name = "Test" };
        var context = new KyrolusValidationContext(Profiles: ["Profile1", "Profile2"], RuleSets: ["Context1"], MinimumSeverity: KyrolusValidationSeverity.Error);

        // Act
        var failures = await validationEngine.ValidateAsync(request, context);

        // Assert
        failures.ShouldNotBeNull();
        failures.Count.ShouldBe(3);
        failures.ShouldContain(f => f.RuleSet == "RuleSetA" && f.Severity == KyrolusValidationSeverity.Error);
        failures.ShouldContain(f => f.RuleSet == "RuleSetB" && f.Severity == KyrolusValidationSeverity.Error);
        failures.ShouldContain(f => f.RuleSet == "Context1" && f.Severity == KyrolusValidationSeverity.Error);
    }
    #endregion

    #region ValidateBatchAsync
    [Fact(DisplayName = "ValidateBatchAsync prefixes each failure's FieldPath with the originating item's index")]
    public async Task ValidateBatchAsync_PrefixesFieldPath_WithItemIndex()
    {
        var serviceProvider = TestHelper.BuildServiceProviderWithValidationRuntime(services =>
            services.AddSingleton<IKyrolusRequestValidator<TestRequest>, TestValidator>()
        );
        var validationEngine = serviceProvider.GetRequiredService<IKyrolusValidationEngine>();

        var requests = new[]
        {
            new TestRequest { Name = "Valid Name", Age = 25 },
            new TestRequest { Name = "", Age = 30 },
            new TestRequest { Name = "Also Valid", Age = -1 }
        };

        var failures = await validationEngine.ValidateBatchAsync(requests);

        failures.Count.ShouldBe(2);
        failures.ShouldContain(f => f.PropertyName == nameof(TestRequest.Name) && f.FieldPath == "[1].Name");
        failures.ShouldContain(f => f.PropertyName == nameof(TestRequest.Age) && f.FieldPath == "[2].Age");
    }

    [Fact(DisplayName = "ValidateBatchAsync returns an empty list when every item in the batch is valid")]
    public async Task ValidateBatchAsync_ReturnsEmptyList_WhenAllItemsValid()
    {
        var serviceProvider = TestHelper.BuildServiceProviderWithValidationRuntime(services =>
            services.AddSingleton<IKyrolusRequestValidator<TestRequest>, TestValidator>()
        );
        var validationEngine = serviceProvider.GetRequiredService<IKyrolusValidationEngine>();

        var requests = new[]
        {
            new TestRequest { Name = "Valid", Age = 1 },
            new TestRequest { Name = "Also Valid", Age = 2 }
        };

        var failures = await validationEngine.ValidateBatchAsync(requests);

        failures.ShouldBeEmpty();
    }

    [Fact(DisplayName = "ValidateBatchAsync applies the same context to every item")]
    public async Task ValidateBatchAsync_AppliesSameContext_ToEveryItem()
    {
        var serviceProvider = TestHelper.BuildServiceProviderWithValidationRuntime(services =>
            services.AddSingleton<IKyrolusRequestValidator<ProfileTestRequest>, ProfileTestValidator>()
        );
        var validationEngine = serviceProvider.GetRequiredService<IKyrolusValidationEngine>();

        var requests = new[] { new ProfileTestRequest { Name = "A" }, new ProfileTestRequest { Name = "B" } };
        var context = new KyrolusValidationContext(RuleSets: ["RuleSetA"]);

        var failures = await validationEngine.ValidateBatchAsync(requests, context);

        // ProfileTestValidator always reports 2 RuleSetA failures (Prop1, Prop2) per item.
        failures.Count.ShouldBe(4);
        failures.ShouldContain(f => f.FieldPath == "[0].Prop1");
        failures.ShouldContain(f => f.FieldPath == "[1].Prop1");
    }
    #endregion

    #region Hooks
    [Fact(DisplayName = "ValidateAsync should execute both Global and Request-Specific Hooks for Before and After validation")]
    public async Task ValidateAsync_ShouldExecuteGlobalAndRequestSpecificHooks()
    {
        // Arrange
        var globalHook = new TestGlobalValidationHook();
        var requestSpecificHook = new TestRequestSpecificValidationHook();

        var serviceProvider = TestHelper.BuildServiceProviderWithValidationRuntime(services =>
        {
            services.AddSingleton<IKyrolusValidationHook>(globalHook);
            services.AddSingleton<IKyrolusValidationHook<HookTestRequest>>(requestSpecificHook);
        });

        var validationEngine = serviceProvider.GetRequiredService<IKyrolusValidationEngine>();

        var request = new HookTestRequest { Id = 42 };
        var context = new KyrolusValidationContext();

        // Act
        var failures = await validationEngine.ValidateAsync(request, context);

        // Assert
        globalHook.OnBeforeCalled.ShouldBeTrue();
        globalHook.OnAfterCalled.ShouldBeTrue();
        globalHook.PassedRequest.ShouldBe(request);
        globalHook.PassedContext.ShouldBe(context);

        requestSpecificHook.OnBeforeCalled.ShouldBeTrue();
        requestSpecificHook.OnAfterCalled.ShouldBeTrue();
        requestSpecificHook.PassedRequest.ShouldBe(request);
        requestSpecificHook.PassedContext.ShouldBe(context);
    }

    [Fact(DisplayName = "ValidateAsync should still run After hooks (and rethrow) when a validator throws")]
    public async Task ValidateAsync_ShouldRunAfterHooks_WhenValidatorThrows()
    {
        var globalHook = new TestGlobalValidationHook();
        var throwingValidator = new ThrowingTestValidator();

        var serviceProvider = TestHelper.BuildServiceProviderWithValidationRuntime(services =>
        {
            services.AddSingleton<IKyrolusValidationHook>(globalHook);
            services.AddSingleton<IKyrolusRequestValidator<TestRequest>>(throwingValidator);
        });

        var validationEngine = serviceProvider.GetRequiredService<IKyrolusValidationEngine>();
        var request = new TestRequest { Name = "x", Age = 1 };

        await Should.ThrowAsync<InvalidOperationException>(async () =>
            await validationEngine.ValidateAsync(request));

        globalHook.OnBeforeCalled.ShouldBeTrue();
        globalHook.OnAfterCalled.ShouldBeTrue();
    }
    #endregion

    #region Caching
    [Fact(DisplayName = "ValidateAsync should store failures in cache on first execution and retrieve from cache on second execution")]
    public async Task ValidateAsync_StoresInCacheAndRetrievesFromCache()
    {
        // Arrange
        var validator = new CacheableTestValidator();
        var serviceProvider = TestHelper.BuildServiceProviderWithValidationRuntime(services =>
        {
            services.AddSingleton<IKyrolusRequestValidator<CacheableTestRequest>>(validator);
        });

        var validationEngine = serviceProvider.GetRequiredService<IKyrolusValidationEngine>();
        var cacheStore = serviceProvider.GetRequiredService<IKyrolusValidationCacheStore>();

        var request = new CacheableTestRequest { CacheKey = "user-123-validation" };
        var context = new KyrolusValidationContext();

        var failures1 = await validationEngine.ValidateAsync(request, context);

        validator.ExecutionCount.ShouldBe(1);
        failures1.ShouldNotBeNull();
        failures1.Count.ShouldBe(1);
        failures1[0].ErrorMessage.ShouldBe("Cached failure message");

        var cachedFailures = await cacheStore.TryGetAsync("user-123-validation");
        cachedFailures.ShouldNotBeNull();
        cachedFailures.Count.ShouldBe(1);

        var failures2 = await validationEngine.ValidateAsync(request, context);

        validator.ExecutionCount.ShouldBe(1);
        failures2.ShouldNotBeNull();
        failures2.Count.ShouldBe(1);
        failures2[0].ErrorMessage.ShouldBe("Cached failure message");
    }

    [Fact(DisplayName = "ValidateAsync localizes cached failures when a localizer is registered")]
    public async Task ValidateAsync_LocalizesCachedFailures_WhenLocalizerIsRegistered()
    {
        // Arrange
        var validator = new CacheableTestValidator();
        var localizer = new TestLocalizer();

        var serviceProvider = TestHelper.BuildServiceProviderWithValidationRuntime(services =>
        {
            services.AddSingleton<IKyrolusRequestValidator<CacheableTestRequest>>(validator);
            services.AddSingleton<IKyrolusLocalizer>(localizer);
        });

        var validationEngine = serviceProvider.GetRequiredService<IKyrolusValidationEngine>();
        var request = new CacheableTestRequest { CacheKey = "user-localized-cache" };
        var context = new KyrolusValidationContext();

        await validationEngine.ValidateAsync(request, context);

        var cachedFailures = await validationEngine.ValidateAsync(request, context);

        // Assert
        cachedFailures.ShouldNotBeNull();
        cachedFailures.Count.ShouldBe(1);
        cachedFailures[0].ErrorMessage.ShouldBe("Localized: Cached failure message");
    }
    #endregion

    #region ContextValidator
    [Fact(DisplayName = "ValidateAsync executes ValidateAsync with context when validator implements IKyrolusRequestValidatorWithContext")]
    public async Task ValidateAsync_ExecutesValidatorWithContext_WhenValidatorImplementsIKyrolusRequestValidatorWithContext()
    {
        // Arrange
        var validator = new ContextValidatorTestValidator();
        var serviceProvider = TestHelper.BuildServiceProviderWithValidationRuntime(services =>
        {
            services.AddSingleton<IKyrolusRequestValidator<ContextValidatorTestRequest>>(validator);
        });

        var validationEngine = serviceProvider.GetRequiredService<IKyrolusValidationEngine>();
        var request = new ContextValidatorTestRequest { Title = "Test Title" };
        var context = new KyrolusValidationContext(RuleSets: ["RuleSetA"]);

        // Act
        var failures = await validationEngine.ValidateAsync(request, context);

        // Assert
        failures.ShouldNotBeNull();
        failures.Count.ShouldBe(1);
        failures[0].ErrorMessage.ShouldBe("Context Validator Failure");
        validator.ReceivedContext.ShouldNotBeNull();
        validator.ReceivedContext!.RuleSets!.ShouldContain("RuleSetA");
    }
    #endregion

    #region ValidateCompositeAsync
    [Fact(DisplayName = "ValidateCompositeAsync validates two objects composite successfully")]
    public async Task ValidateCompositeAsync_ValidatesTwoObjectsComposite()
    {
        // Arrange
        var validator = new CompositeTestValidator();
        var serviceProvider = TestHelper.BuildServiceProviderWithValidationRuntime(services =>
        {
            services.AddSingleton<IKyrolusRequestValidator<KyrolusValidationComposite<TestRequest, ProfileTestRequest>>>(validator);
        });

        var validationEngine = serviceProvider.GetRequiredService<IKyrolusValidationEngine>();
        var first = new TestRequest { Name = "Valid", Age = 30 };
        var second = new ProfileTestRequest { Name = "Valid" };

        // Act
        var failures = await validationEngine.ValidateCompositeAsync(first, second);

        // Assert
        failures.ShouldNotBeNull();
        failures.Count.ShouldBe(1);
        failures[0].ErrorMessage.ShouldBe("Composite validation failed");
    }

    [Fact(DisplayName = "ValidateCompositeAsync with context validates composite successfully")]
    public async Task ValidateCompositeAsync_WithContext_ValidatesComposite()
    {
        // Arrange
        var validator = new CompositeTestValidator();
        var serviceProvider = TestHelper.BuildServiceProviderWithValidationRuntime(services =>
        {
            services.AddSingleton<IKyrolusRequestValidator<KyrolusValidationComposite<TestRequest, ProfileTestRequest>>>(validator);
        });

        var validationEngine = serviceProvider.GetRequiredService<IKyrolusValidationEngine>();
        var first = new TestRequest { Name = "Valid", Age = 30 };
        var second = new ProfileTestRequest { Name = "Valid" };
        var context = new KyrolusValidationContext();

        // Act
        var failures = await validationEngine.ValidateCompositeAsync(first, second, context);

        // Assert
        failures.ShouldNotBeNull();
        failures.Count.ShouldBe(1);
        failures[0].ErrorMessage.ShouldBe("Composite validation failed");
    }

    [Fact(DisplayName = "ValidateCompositeAsync 3 and 4 items overloads return empty when no composite validator registered")]
    public async Task ValidateCompositeAsync_ThreeAndFourItemsOverloads_ReturnEmptyWhenNoValidatorRegistered()
    {
        // Arrange
        var serviceProvider = TestHelper.BuildServiceProviderWithValidationRuntime();
        var validationEngine = serviceProvider.GetRequiredService<IKyrolusValidationEngine>();

        var first = new TestRequest();
        var second = new ProfileTestRequest();
        var third = new HookTestRequest();
        var fourth = new ContextValidatorTestRequest();

        // Act & Assert (3 items)
        var failures3 = await validationEngine.ValidateCompositeAsync(first, second, third);
        failures3.ShouldNotBeNull();
        failures3.ShouldBeEmpty();

        var failures3WithContext = await validationEngine.ValidateCompositeAsync(first, second, third, new KyrolusValidationContext());
        failures3WithContext.ShouldNotBeNull();
        failures3WithContext.ShouldBeEmpty();

        // Act & Assert (4 items)
        var failures4 = await validationEngine.ValidateCompositeAsync(first, second, third, fourth);
        failures4.ShouldNotBeNull();
        failures4.ShouldBeEmpty();

        var failures4WithContext = await validationEngine.ValidateCompositeAsync(first, second, third, fourth, new KyrolusValidationContext());
        failures4WithContext.ShouldNotBeNull();
        failures4WithContext.ShouldBeEmpty();
    }
    #endregion

    #region Groups Profiles
    [Fact(DisplayName = "ValidateAsync should apply profiles and merge groups with context")]
    public async Task ValidateAsync_ShouldApplyProfilesAndMergeGroupsWithContext()
    {
        // Arrange
        var profile1 = new KyrolusValidationProfile(
            "GroupProfile1",
            new KyrolusValidationContext(Groups: ["GroupA"])
        );

        var profile2 = new KyrolusValidationProfile(
            "GroupProfile2",
            new KyrolusValidationContext(Groups: ["GroupB"])
        );

        var serviceProvider = TestHelper.BuildServiceProviderWithValidationRuntime(services =>
        {
            services.AddKyrolusValidationProfiles(profile1, profile2);
            services.AddSingleton<IKyrolusRequestValidator<GroupTestRequest>, GroupTestValidator>();
        });

        var validationEngine = serviceProvider.GetRequiredService<IKyrolusValidationEngine>();
        var request = new GroupTestRequest { Name = "Test" };
        var context = new KyrolusValidationContext(Profiles: ["GroupProfile1", "GroupProfile2"], Groups: ["ContextGroup"]);

        // Act
        var failures = await validationEngine.ValidateAsync(request, context);

        // Assert
        // Merged Groups: ["GroupA", "GroupB", "ContextGroup"] (OtherGroup is filtered out)
        failures.ShouldNotBeNull();
        failures.Count.ShouldBe(3);
        failures.ShouldContain(f => f.Groups != null && f.Groups.Contains("GroupA"));
        failures.ShouldContain(f => f.Groups != null && f.Groups.Contains("GroupB"));
        failures.ShouldContain(f => f.Groups != null && f.Groups.Contains("ContextGroup"));
    }
    #endregion

    #region Mappings
    [Fact(DisplayName = "ValidateAsync applies error code and field path dictionary mappings when mappers are registered")]
    public async Task ValidateAsync_AppliesErrorCodeAndFieldPathMappings_WhenMappersAreRegistered()
    {
        // Arrange
        var codeMapper = new KyrolusDictionaryValidationErrorCodeMapper(new Dictionary<string, string>
        {
            ["ERR_AGE"] = "USER_INVALID_AGE"
        });

        var pathMapper = new KyrolusDictionaryValidationFieldPathMapper(new Dictionary<string, string>
        {
            ["Age"] = "user_age"
        });

        var serviceProvider = TestHelper.BuildServiceProviderWithValidationRuntime(services =>
        {
            services.AddSingleton<IKyrolusRequestValidator<MappingTestRequest>, MappingTestValidator>();
            services.AddSingleton<IKyrolusValidationErrorCodeMapper>(codeMapper);
            services.AddSingleton<IKyrolusValidationFieldPathMapper>(pathMapper);
        });

        var validationEngine = serviceProvider.GetRequiredService<IKyrolusValidationEngine>();
        var request = new MappingTestRequest();
        var context = new KyrolusValidationContext();

        // Act
        var failures = await validationEngine.ValidateAsync(request, context);

        // Assert
        failures.ShouldNotBeNull();
        failures.Count.ShouldBe(1);
        failures[0].ErrorCode.ShouldBe("USER_INVALID_AGE");
        failures[0].FieldPath.ShouldBe("user_age");
    }

    [Fact(DisplayName = "ValidateAsync applies delegate error code and field path mappings")]
    public async Task ValidateAsync_AppliesDelegateMappings()
    {
        // Arrange
        var codeMapper = new KyrolusDelegateValidationErrorCodeMapper((failure, ctx) => $"DELEGATE_{failure.ErrorCode}");
        var pathMapper = new KyrolusDelegateValidationFieldPathMapper((failure, ctx) => $"payload.{failure.PropertyName?.ToLowerInvariant()}");

        var serviceProvider = TestHelper.BuildServiceProviderWithValidationRuntime(services =>
        {
            services.AddSingleton<IKyrolusRequestValidator<MappingTestRequest>, MappingTestValidator>();
            services.AddSingleton<IKyrolusValidationErrorCodeMapper>(codeMapper);
            services.AddSingleton<IKyrolusValidationFieldPathMapper>(pathMapper);
        });

        var validationEngine = serviceProvider.GetRequiredService<IKyrolusValidationEngine>();
        var request = new MappingTestRequest();
        var context = new KyrolusValidationContext();

        // Act
        var failures = await validationEngine.ValidateAsync(request, context);

        // Assert
        failures.ShouldNotBeNull();
        failures.Count.ShouldBe(1);
        failures[0].ErrorCode.ShouldBe("DELEGATE_ERR_AGE");
        failures[0].FieldPath.ShouldBe("payload.age");
    }

    [Fact(DisplayName = "ValidateAsync falls back to original ErrorCode and FieldPath when mappers return null or whitespace")]
    public async Task ValidateAsync_FallsBackToOriginalErrorCodeAndFieldPath_WhenMappersReturnNullOrWhitespace()
    {
        // Arrange
        var nullCodeMapper = new KyrolusDelegateValidationErrorCodeMapper((failure, ctx) => null);
        var whitespacePathMapper = new KyrolusDelegateValidationFieldPathMapper((failure, ctx) => "   ");

        var serviceProvider = TestHelper.BuildServiceProviderWithValidationRuntime(services =>
        {
            services.AddSingleton<IKyrolusRequestValidator<MappingTestRequest>, MappingTestValidator>();
            services.AddSingleton<IKyrolusValidationErrorCodeMapper>(nullCodeMapper);
            services.AddSingleton<IKyrolusValidationFieldPathMapper>(whitespacePathMapper);
        });

        var validationEngine = serviceProvider.GetRequiredService<IKyrolusValidationEngine>();
        var request = new MappingTestRequest();
        var context = new KyrolusValidationContext();

        // Act
        var failures = await validationEngine.ValidateAsync(request, context);

        // Assert
        failures.ShouldNotBeNull();
        failures.Count.ShouldBe(1);
        failures[0].ErrorCode.ShouldBe("ERR_AGE");
    }

    [Fact(DisplayName = "ValidateAsync handles single mapper registered scenario")]
    public async Task ValidateAsync_HandlesSingleMapperRegisteredScenario()
    {
        // Arrange
        var codeMapper = new KyrolusDictionaryValidationErrorCodeMapper(new Dictionary<string, string>
        {
            ["ERR_AGE"] = "SINGLE_MAPPED_CODE"
        });

        var serviceProvider = TestHelper.BuildServiceProviderWithValidationRuntime(services =>
        {
            services.AddSingleton<IKyrolusRequestValidator<MappingTestRequest>, MappingTestValidator>();
            services.AddSingleton<IKyrolusValidationErrorCodeMapper>(codeMapper);
        });

        var validationEngine = serviceProvider.GetRequiredService<IKyrolusValidationEngine>();
        var request = new MappingTestRequest();
        var context = new KyrolusValidationContext();

        // Act
        var failures = await validationEngine.ValidateAsync(request, context);

        // Assert
        failures.ShouldNotBeNull();
        failures.Count.ShouldBe(1);
        failures[0].ErrorCode.ShouldBe("SINGLE_MAPPED_CODE");
    }

    [Fact(DisplayName = "ValidateAsync passes effective context to error code and field path mappers")]
    public async Task ValidateAsync_PassesEffectiveContext_ToErrorCodeAndFieldPathMappers()
    {
        // Arrange
        KyrolusValidationContext? receivedCodeContext = null;
        KyrolusValidationContext? receivedPathContext = null;

        var codeMapper = new KyrolusDelegateValidationErrorCodeMapper((failure, ctx) =>
        {
            receivedCodeContext = ctx;
            return "CONTEXT_VERIFIED_CODE";
        });

        var pathMapper = new KyrolusDelegateValidationFieldPathMapper((failure, ctx) =>
        {
            receivedPathContext = ctx;
            return "context_verified_path";
        });

        var serviceProvider = TestHelper.BuildServiceProviderWithValidationRuntime(services =>
        {
            services.AddSingleton<IKyrolusRequestValidator<MappingTestRequest>, MappingTestValidator>();
            services.AddSingleton<IKyrolusValidationErrorCodeMapper>(codeMapper);
            services.AddSingleton<IKyrolusValidationFieldPathMapper>(pathMapper);
        });

        var validationEngine = serviceProvider.GetRequiredService<IKyrolusValidationEngine>();
        var request = new MappingTestRequest();
        var context = new KyrolusValidationContext(MinimumSeverity: KyrolusValidationSeverity.Info);

        // Act
        var failures = await validationEngine.ValidateAsync(request, context);

        // Assert
        failures.ShouldNotBeNull();
        failures.Count.ShouldBe(1);
        failures[0].ErrorCode.ShouldBe("CONTEXT_VERIFIED_CODE");
        failures[0].FieldPath.ShouldBe("context_verified_path");

        receivedCodeContext.ShouldNotBeNull();
        receivedCodeContext!.MinimumSeverity.ShouldBe(KyrolusValidationSeverity.Info);

        receivedPathContext.ShouldNotBeNull();
        receivedPathContext!.MinimumSeverity.ShouldBe(KyrolusValidationSeverity.Info);
    }
    #endregion




    #region Negative Caching
    [Fact(DisplayName = "ValidateAsync resolves custom negative cache TTL when request implements IKyrolusValidationNegativeCacheable")]
    public async Task ValidateAsync_ResolvesNegativeTtl_WhenRequestImplementsIKyrolusValidationNegativeCacheable()
    {
        // Arrange
        var serviceProvider = TestHelper.BuildServiceProviderWithValidationRuntime();
        var validationEngine = serviceProvider.GetRequiredService<IKyrolusValidationEngine>();
        var cacheStore = serviceProvider.GetRequiredService<IKyrolusValidationCacheStore>();

        var request = new NegativeCacheableTestRequest { CacheKey = "custom-negative-ttl-key" };
        var context = new KyrolusValidationContext();

        var failures = await validationEngine.ValidateAsync(request, context);

        failures.ShouldNotBeNull();
        failures.ShouldBeEmpty();

        var cachedFailures = await cacheStore.TryGetAsync("custom-negative-ttl-key");
        cachedFailures.ShouldNotBeNull();
        cachedFailures.ShouldBeEmpty();
    }
    #endregion

    #region CancellationToken
    [Fact(DisplayName = "ValidateAsync passes CancellationToken to registered validators and hooks")]
    public async Task ValidateAsync_PassesCancellationToken_ToValidatorAndHooks()
    {
        // Arrange
        var validator = new CancellationTokenTestValidator();
        var globalHook = new TestGlobalValidationHook();

        var serviceProvider = TestHelper.BuildServiceProviderWithValidationRuntime(services =>
        {
            services.AddSingleton<IKyrolusRequestValidator<CancellationTokenTestRequest>>(validator);
            services.AddSingleton<IKyrolusValidationHook>(globalHook);
        });

        var validationEngine = serviceProvider.GetRequiredService<IKyrolusValidationEngine>();
        var request = new CancellationTokenTestRequest { Data = "Test" };
        var context = new KyrolusValidationContext();
        using var cts = new CancellationTokenSource();

        // Act
        var failures = await validationEngine.ValidateAsync(request, context, cts.Token);

        // Assert
        failures.ShouldNotBeNull();
        failures.ShouldBeEmpty();
        validator.ReceivedToken.ShouldBe(cts.Token);
    }

    [Fact(DisplayName = "ValidateAsync throws OperationCanceledException when CancellationToken is canceled")]
    public async Task ValidateAsync_ThrowsOperationCanceledException_WhenCancellationTokenIsCanceled()
    {
        // Arrange
        var validator = new CancellationTokenTestValidator();

        var serviceProvider = TestHelper.BuildServiceProviderWithValidationRuntime(services =>
        {
            services.AddSingleton<IKyrolusRequestValidator<CancellationTokenTestRequest>>(validator);
        });

        var validationEngine = serviceProvider.GetRequiredService<IKyrolusValidationEngine>();
        var request = new CancellationTokenTestRequest { Data = "Test" };
        var context = new KyrolusValidationContext();
        using var cts = new CancellationTokenSource();
        cts.Cancel(); 
        // Act & Assert
        await Should.ThrowAsync<OperationCanceledException>(async () =>
        {
            await validationEngine.ValidateAsync(request, context, cts.Token);
        });
    }
    #endregion

    #region Cache Modes (SuccessOnly & FailuresOnly)
    [Fact(DisplayName = "ValidateAsync stores in cache when mode is SuccessOnly and no failures occur")]
    public async Task ValidateAsync_CacheMode_SuccessOnly_StoresOnlyWhenSuccess()
    {
        // Arrange
        var serviceProvider = TestHelper.BuildServiceProviderWithValidationRuntime();
        var validationEngine = serviceProvider.GetRequiredService<IKyrolusValidationEngine>();
        var cacheStore = serviceProvider.GetRequiredService<IKyrolusValidationCacheStore>();

        var request = new SuccessOnlyCacheableTestRequest { CacheKey = "success-only-key-1" };
        var context = new KyrolusValidationContext();

        // Act - لا توجد أخطاء (نجاح)
        var failures = await validationEngine.ValidateAsync(request, context);

        // Assert - النتيجة النجاحية اتخزنت في الكاش
        failures.ShouldBeEmpty();
        var cachedFailures = await cacheStore.TryGetAsync("success-only-key-1");
        cachedFailures.ShouldNotBeNull();
        cachedFailures.ShouldBeEmpty();
    }

    [Fact(DisplayName = "ValidateAsync stores in cache when mode is FailuresOnly and failures occur")]
    public async Task ValidateAsync_CacheMode_FailuresOnly_StoresOnlyWhenFailures()
    {
        // Arrange
        var validator = new FailuresOnlyCacheableTestValidator();
        var serviceProvider = TestHelper.BuildServiceProviderWithValidationRuntime(services =>
        {
            services.AddSingleton<IKyrolusRequestValidator<FailuresOnlyCacheableTestRequest>>(validator);
        });

        var validationEngine = serviceProvider.GetRequiredService<IKyrolusValidationEngine>();
        var cacheStore = serviceProvider.GetRequiredService<IKyrolusValidationCacheStore>();

        var request = new FailuresOnlyCacheableTestRequest { CacheKey = "failures-only-key-1" };
        var context = new KyrolusValidationContext();

        // Act - توجد أخطاء تحقق
        var failures = await validationEngine.ValidateAsync(request, context);

        // Assert - قائمة الأخطاء اتخزنت في الكاش بموجب مدة FailuresOnly
        failures.Count.ShouldBe(1);
        var cachedFailures = await cacheStore.TryGetAsync("failures-only-key-1");
        cachedFailures.ShouldNotBeNull();
        cachedFailures.Count.ShouldBe(1);
    }

    [Fact(DisplayName = "ValidateAsync does not store in cache when mode is None")]
    public async Task ValidateAsync_CacheMode_None_DoesNotStoreInCache()
    {
        // Arrange
        var serviceProvider = TestHelper.BuildServiceProviderWithValidationRuntime();
        var validationEngine = serviceProvider.GetRequiredService<IKyrolusValidationEngine>();
        var cacheStore = serviceProvider.GetRequiredService<IKyrolusValidationCacheStore>();

        var request = new CacheableCacheModeIsNoneRequest();
        var context = new KyrolusValidationContext();

        // Act
        var failures = await validationEngine.ValidateAsync(request, context);

        // Assert - النتيجة لم تتخزن في الكاش لأن CacheMode == None
        failures.ShouldBeEmpty();
        (await cacheStore.TryGetAsync("NONEMODECACHEKEY")).ShouldBeNull();
    }

    [Fact(DisplayName = "ValidateAsync does not store in cache when CacheTtl is Zero")]
    public async Task ValidateAsync_CacheTtl_IsZero_DoesNotStoreInCache()
    {
        // Arrange
        var serviceProvider = TestHelper.BuildServiceProviderWithValidationRuntime();
        var validationEngine = serviceProvider.GetRequiredService<IKyrolusValidationEngine>();
        var cacheStore = serviceProvider.GetRequiredService<IKyrolusValidationCacheStore>();

        var request = new ZeroTtlCacheableTestRequest();
        var context = new KyrolusValidationContext();

        // Act
        var failures = await validationEngine.ValidateAsync(request, context);

        // Assert - النتيجة لم تتخزن في الكاش لأن CacheTtl == TimeSpan.Zero
        failures.ShouldBeEmpty();
        (await cacheStore.TryGetAsync("ZEROTTLKEY")).ShouldBeNull();
    }
    #endregion



    #region Null RuleSet and Group Filtering Defaults
    [Fact(DisplayName = "ValidateAsync filters failures with null RuleSet and null Group using defaults")]
    public async Task ValidateAsync_FiltersFailuresWithNullRuleSetAndNullGroup_UsingDefaults()
    {
        // Arrange
        var validator = new NullRuleSetAndGroupValidator();
        var serviceProvider = TestHelper.BuildServiceProviderWithValidationRuntime(services =>
        {
            services.AddSingleton<IKyrolusRequestValidator<NullRuleSetAndGroupRequest>>(validator);
        });

        var validationEngine = serviceProvider.GetRequiredService<IKyrolusValidationEngine>();
        var request = new NullRuleSetAndGroupRequest();

        // Act 1: فحص الـ RuleSet عندما يكون null مع سياق يحتوي على "default"
        var ruleSetContext = new KyrolusValidationContext(RuleSets: [KyrolusValidationDefaults.DefaultRuleSet]);
        var ruleSetFailures = await validationEngine.ValidateAsync(request, ruleSetContext);

        // Assert 1: تمرير الـ DefaultRuleSet نجح في مطابقة الـ failure.RuleSet ?? "default"
        ruleSetFailures.ShouldNotBeNull();
        ruleSetFailures.Count.ShouldBe(1);

        // Act 2: فحص الـ Group عندما يكون null مع سياق يحتوي على "default"
        var groupContext = new KyrolusValidationContext(Groups: [KyrolusValidationDefaults.DefaultGroup]);
        var groupFailures = await validationEngine.ValidateAsync(request, groupContext);

        // Assert 2: تمرير الـ DefaultGroup نجح في مطابقة الـ failure.Group ?? "default"
        groupFailures.ShouldNotBeNull();
        groupFailures.Count.ShouldBe(1);
    }
    #endregion
}









