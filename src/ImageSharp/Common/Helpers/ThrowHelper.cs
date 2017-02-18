namespace ImageSharp
{
    using System;
    using System.Runtime.CompilerServices;

    /// <summary>
    /// Helps removing exception throwing code from hot path by providing non-inlined exception thrower methods.
    /// </summary>
    internal static class ThrowHelper
    {
        [MethodImpl(MethodImplOptions.NoInlining)]
        public static void ThrowArgumentNullException(string paramName)
        {
            throw new ArgumentNullException(nameof(paramName));
        }
    }
}