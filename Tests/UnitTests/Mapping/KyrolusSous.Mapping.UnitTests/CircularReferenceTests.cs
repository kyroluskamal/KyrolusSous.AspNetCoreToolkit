namespace KyrolusSous.Mapping.UnitTests;

public sealed class CircularReferenceTests
{
    private sealed class ParentEntity
    {
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public ChildEntity? Child { get; set; }
    }

    private sealed class ChildEntity
    {
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public ParentEntity? Parent { get; set; }
    }

    private sealed class ParentDto
    {
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public ChildDto? Child { get; set; }
    }

    private sealed class ChildDto
    {
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public ParentDto? Parent { get; set; }
    }

    [Fact(DisplayName = "KyrolusObjectMapper: Handles circular object references safely without StackOverflowException")]
    public void CircularReferences_HandledSafely()
    {
        var mapper = new KyrolusObjectMapper();

        var parent = new ParentEntity { Id = 1, Name = "Father" };
        var child = new ChildEntity { Id = 2, Name = "Son", Parent = parent };
        parent.Child = child;

        // Perform mapping on recursive graph
        var parentDto = mapper.Map<ParentEntity, ParentDto>(parent);

        parentDto.ShouldNotBeNull();
        parentDto.Id.ShouldBe(1);
        parentDto.Name.ShouldBe("Father");
        parentDto.Child.ShouldNotBeNull();
        parentDto.Child.Id.ShouldBe(2);
        parentDto.Child.Name.ShouldBe("Son");

        // Identity check: child.Parent should be the exact same instance as parentDto
        parentDto.Child.Parent.ShouldBeSameAs(parentDto);
    }
}
