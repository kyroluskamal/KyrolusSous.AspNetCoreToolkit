// Source generators target netstandard2.0, which predates records. The compiler emits `init`
// accessors in terms of this marker type, and netstandard2.0 does not ship it - declaring it here
// is the standard polyfill and lets the generator use records for its pipeline models.
//
// Internal, so it never leaks into a consuming project.

namespace System.Runtime.CompilerServices
{
    internal static class IsExternalInit
    {
    }
}
