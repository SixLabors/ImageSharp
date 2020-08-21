// Copyright (c) Six Labors.
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
#if NETCOREAPP3_1
            "netcoreapp3.1";
#elif NETCOREAPP2_1
            "netcoreapp2.1";
#elif NETSTANDARD2_1
            "netstandard2.1";
#elif NETSTANDARD2_0
            "netstandard2.0";
#elif NETSTANDARD1_3
            "netstandard1.3";
#else
            "net472";
#endif
    }
}
