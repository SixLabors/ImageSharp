// Copyright(c) Six Labors and contributors.
// Licensed under the Apache License, Version 2.0.

namespace SixLabors.ImageSharp.Common.Helpers
{
    /// <summary>
    /// Internal utilities intended to be only used in tests.
    /// </summary>
    internal static class TestHelpers
    {
        /// <summary>
        /// This constant is useful to verify the target framework ImageSharp has been built against.
        /// Only intended to be used in tests!
        /// </summary>
        internal const string ImageSharpBuiltAgainst =
#if NET472
            "netfx4.7.2";
#elif NETCOREAPP2_1
            "netcoreapp2.1";
#elif NETSTANDARD1_3
            "netstandard1.3";
#else
            "netstandard2.0";
#endif
    }
}