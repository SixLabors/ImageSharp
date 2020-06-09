// Copyright (c) Six Labors.
// Licensed under the Apache License, Version 2.0.

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
