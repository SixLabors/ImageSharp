// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

// Uncomment to enable local profiling benchmarks. DO NOT PUSH TO MAIN!
// #define PROFILING
namespace SixLabors.ImageSharp.Tests.ProfilingBenchmarks
{
    public static class ProfilingSetup
    {
        public const string SkipProfilingTests =
#if PROFILING
            null;
#else
            "Profiling benchmark, enable manually!";
#endif
    }
}
