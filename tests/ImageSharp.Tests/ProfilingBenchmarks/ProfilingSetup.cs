// Copyright (c) Six Labors and contributors.
// Licensed under the GNU Affero General Public License, Version 3.

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
