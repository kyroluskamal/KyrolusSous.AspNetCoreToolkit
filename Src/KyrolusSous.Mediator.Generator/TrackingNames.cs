namespace KyrolusSous.Mediator.Generator
{
    /// <summary>
    /// Names given to steps in the incremental pipeline so tests can inspect them.
    /// </summary>
    /// <remarks>
    /// A test creates the driver with <c>trackIncrementalGeneratorSteps: true</c>, runs the
    /// generator twice, and looks up a step by one of these names in
    /// <c>GeneratorRunResult.TrackedSteps</c>. The reason recorded against each output
    /// (<c>Cached</c>, <c>Unchanged</c>, <c>New</c>, <c>Modified</c>) is what proves whether the
    /// caching is doing anything - an edit that touches no handler should leave this step
    /// <c>Cached</c>.
    /// <para>
    /// Constants rather than literals so a rename cannot silently break a test into always
    /// passing.
    /// </para>
    /// </remarks>
    public static class TrackingNames
    {
        /// <summary>The per-class semantic analysis stage.</summary>
        public const string HandlerModels = nameof(HandlerModels);
    }
}
