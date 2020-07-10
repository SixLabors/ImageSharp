// Copyright (c) Six Labors.
// Licensed under the Apache License, Version 2.0.

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
