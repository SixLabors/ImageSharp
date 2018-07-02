// Copyright (c) Six Labors and contributors.
// Licensed under the Apache License, Version 2.0.

// Uncomment this for verbose profiler results:
// #define PROFILING
using System.Runtime.CompilerServices;

namespace SixLabors.ImageSharp
{
    /// <summary>
    /// Global inlining options. Helps temporarily disable inling for better profiler output.
    /// </summary>
    internal static class InliningOptions
    {
#if PROFILING
        public const MethodImplOptions ShortMethod = 0;
#else
        public const MethodImplOptions ShortMethod = MethodImplOptions.AggressiveInlining;
#endif
        public const MethodImplOptions ColdPath = MethodImplOptions.NoInlining;
    }
}