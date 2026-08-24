namespace KyrolusSous.Mapping.UnitTests;

public sealed class FluentProfileMappingTests
{
    private sealed class SourceUser
    {
        public int Id { get; set; }
        public string FirstName { get; set; } = string.Empty;
        public string LastName { get; set; } = string.Empty;
        public string InternalNote { get; set; } = string.Empty;
    }

    private sealed class TargetUserDto
    {
        public int Id { get; set; }
        public string FullName { get; set; } = string.Empty;
        public string InternalNote { get; set; } = "Default";
    }

    private sealed class UserProfile : KyrolusMappingProfile
    {
        public UserProfile()
        {
            CreateMap<SourceUser, TargetUserDto>()
                .ForMember(dest => dest.FullName, opt => opt.MapFrom(src => $"{src.FirstName} {src.LastName}"))
                .Ignore(dest => dest.InternalNote)
                .ReverseMap();
        }
    }

    [Fact(DisplayName = "KyrolusMappingProfile: Applies custom member expressions and ignores specified members")]
    public void Profile_CustomMemberAndIgnore()
    {
        var config = new KyrolusMappingConfiguration();
        config.AddProfile<UserProfile>();

        var mapper = new KyrolusObjectMapper(config);
        var source = new SourceUser { Id = 1, FirstName = "Kyrolus", LastName = "Sous", InternalNote = "Confidential" };

        var dto = mapper.Map<SourceUser, TargetUserDto>(source);

        dto.ShouldNotBeNull();
        dto.Id.ShouldBe(1);
        dto.FullName.ShouldBe("Kyrolus Sous");
        dto.InternalNote.ShouldBe("Default"); // Ignored
    }

    [Fact(DisplayName = "ServiceCollectionExtensions: AddKyrolusMapping registers IKyrolusObjectMapper in DI")]
    public void ServiceCollection_Registration_Works()
    {
        var services = new ServiceCollection();
        services.AddKyrolusMapping(cfg =>
        {
            cfg.AddProfile<UserProfile>();
        });

        var sp = services.BuildServiceProvider();
        var mapper = sp.GetService<IKyrolusObjectMapper>();

        mapper.ShouldNotBeNull();
        mapper.ShouldBeOfType<KyrolusObjectMapper>();

        var dto = mapper.Map<SourceUser, TargetUserDto>(new SourceUser { Id = 2, FirstName = "John", LastName = "Smith" });
        dto.FullName.ShouldBe("John Smith");
    }
}
