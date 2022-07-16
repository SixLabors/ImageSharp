// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

// Uncomment this for verbose profiler results. DO NOT PUSH TO MAIN!
// #define PROFILING
using System.Runtime.CompilerServices;

namespace SixLabors.ImageSharp
{
    /// <summary>
    /// Global inlining options. Helps temporarily disable inlining for better profiler output.
    /// </summary>
    internal static class InliningOptions
    {
        /// <summary>
        /// <see cref="MethodImplOptions.AggressiveInlining"/> regardless of the build conditions.
        /// </summary>
        public const MethodImplOptions AlwaysInline = MethodImplOptions.AggressiveInlining;
#if PROFILING
        public const MethodImplOptions HotPath = MethodImplOptions.NoInlining;
        public const MethodImplOptions ShortMethod = MethodImplOptions.NoInlining;
#else
#if SUPPORTS_HOTPATH
        public const MethodImplOptions HotPath = MethodImplOptions.AggressiveOptimization;
#else
        public const MethodImplOptions HotPath = MethodImplOptions.AggressiveInlining;
#endif
        public const MethodImplOptions ShortMethod = MethodImplOptions.AggressiveInlining;
#endif
        public const MethodImplOptions ColdPath = MethodImplOptions.NoInlining;
    }
}
