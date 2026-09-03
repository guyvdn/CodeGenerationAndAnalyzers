// Polyfill: `init` accessors (used by the readonly record struct model) need this
// type, which netstandard2.0 doesn't ship. Compiling it in is the standard fix.
namespace System.Runtime.CompilerServices
{
    using System.ComponentModel;

    [EditorBrowsable(EditorBrowsableState.Never)]
    internal static class IsExternalInit
    {
    }
}
