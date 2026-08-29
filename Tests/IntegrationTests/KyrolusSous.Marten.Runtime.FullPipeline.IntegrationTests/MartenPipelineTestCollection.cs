using Xunit;

namespace KyrolusSous.Marten.Runtime.FullPipeline.IntegrationTests;

[CollectionDefinition("MartenPipelineTestCollection", DisableParallelization = true)]
public class MartenPipelineTestCollection : ICollectionFixture<TestAppFactory>
{
}
