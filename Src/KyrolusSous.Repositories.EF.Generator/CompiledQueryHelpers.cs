namespace RepositoryLib;

internal static class CompiledQueryHelpers
{
    public static bool CanGenerateCompiledGetById(int keyCount) => keyCount == 1;
}
