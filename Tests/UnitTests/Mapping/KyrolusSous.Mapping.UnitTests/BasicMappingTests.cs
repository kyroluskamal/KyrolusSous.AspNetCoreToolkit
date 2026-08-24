namespace KyrolusSous.Mapping.UnitTests;

public sealed class BasicMappingTests
{
    private enum UserStatus { Inactive = 0, Active = 1, Suspended = 2 }

    private sealed class UserEntity
    {
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public decimal Salary { get; set; }
        public Guid ExternalId { get; set; }
        public UserStatus Status { get; set; }
        public DateTime CreatedAt { get; set; }
        [KyrolusIgnoreMap]
        public string PasswordHash { get; set; } = string.Empty;
    }

    private sealed class UserDto
    {
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public string Salary { get; set; } = string.Empty; // Type conversion decimal -> string
        public string ExternalId { get; set; } = string.Empty; // Type conversion Guid -> string
        public string Status { get; set; } = string.Empty; // Type conversion Enum -> string
        public DateTimeOffset CreatedAt { get; set; } // Type conversion DateTime -> DateTimeOffset
        public string PasswordHash { get; set; } = "DefaultHash";
    }

    [Fact(DisplayName = "KyrolusObjectMapper: Maps matching properties and converts primitive types")]
    public void BasicMapping_MatchingPropertiesAndConversions()
    {
        var mapper = new KyrolusObjectMapper();
        var entity = new UserEntity
        {
            Id = 10,
            Name = "John Doe",
            Salary = 7500.50m,
            ExternalId = Guid.Parse("11111111-2222-3333-4444-555555555555"),
            Status = UserStatus.Active,
            CreatedAt = new DateTime(2026, 1, 15, 10, 30, 0, DateTimeKind.Utc),
            PasswordHash = "SecretHash123"
        };

        var dto = mapper.Map<UserEntity, UserDto>(entity);

        dto.ShouldNotBeNull();
        dto.Id.ShouldBe(10);
        dto.Name.ShouldBe("John Doe");
        dto.Salary.ShouldBe("7500.50");
        dto.ExternalId.ShouldBe("11111111-2222-3333-4444-555555555555");
        dto.Status.ShouldBe("Active");
        dto.CreatedAt.DateTime.ShouldBe(entity.CreatedAt);
        dto.PasswordHash.ShouldBe("DefaultHash"); // Ignored
    }

    [Fact(DisplayName = "KyrolusObjectMapper: In-place mapping mutates existing target without re-instantiation")]
    public void InPlaceMapping_MutatesTarget()
    {
        var mapper = new KyrolusObjectMapper();
        var entity = new UserEntity { Id = 20, Name = "Updated Name", Salary = 9000m };
        var existingDto = new UserDto { Id = 1, Name = "Old Name", Salary = "1000", PasswordHash = "KeepThis" };

        var result = mapper.Map(entity, existingDto);

        result.ShouldBeSameAs(existingDto);
        existingDto.Id.ShouldBe(20);
        existingDto.Name.ShouldBe("Updated Name");
        existingDto.Salary.ShouldBe("9000");
        existingDto.PasswordHash.ShouldBe("KeepThis");
    }

    [Fact(DisplayName = "KyrolusObjectMapper: Weakly typed Map(object) maps properly")]
    public void WeaklyTypedMapping_Works()
    {
        var mapper = new KyrolusObjectMapper();
        object entity = new UserEntity { Id = 30, Name = "Weak User" };

        var dto = mapper.Map<UserDto>(entity);

        dto.ShouldNotBeNull();
        dto.Id.ShouldBe(30);
        dto.Name.ShouldBe("Weak User");
    }

    [Fact(DisplayName = "KyrolusObjectMapper: Null source returns default/null")]
    public void NullSource_ReturnsDefault()
    {
        var mapper = new KyrolusObjectMapper();
        var dto = mapper.Map<UserEntity, UserDto>(null!);
        dto.ShouldBeNull();
    }
}
