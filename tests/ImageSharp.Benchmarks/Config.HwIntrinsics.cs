// Copyright (c) Six Labors.
// Licensed under the Apache License, Version 2.0.

#if SUPPORTS_RUNTIME_INTRINSICS
using System.Runtime.Intrinsics.X86;
#endif
using BenchmarkDotNet.Environments;
using BenchmarkDotNet.Jobs;

namespace SixLabors.ImageSharp.Benchmarks
{
    public partial class Config
    {
        private const string On = "1";
        private const string Off = "0";

        // See https://github.com/SixLabors/ImageSharp/pull/1229#discussion_r440477861
        // * EnableHWIntrinsic
        //   * EnableSSE
        //     * EnableSSE2
        //       * EnableAES
        //       * EnablePCLMULQDQ
        //       * EnableSSE3
        //         * EnableSSSE3
        //           * EnableSSE41
        //             * EnableSSE42
        //               * EnablePOPCNT
        //               * EnableAVX
        //                 * EnableFMA
        //                 * EnableAVX2
        //   * EnableBMI1
        //   * EnableBMI2
        //   * EnableLZCNT
        //
        // `FeatureSIMD` ends up impacting all SIMD support(including `System.Numerics`) but not things
        // like `LZCNT`, `BMI1`, or `BMI2`
        // `EnableSSE3_4` is a legacy switch that exists for compat and is basically the same as `EnableSSE3`
        private const string EnableAES = "COMPlus_EnableAES";
        private const string EnableAVX = "COMPlus_EnableAVX";
        private const string EnableAVX2 = "COMPlus_EnableAVX2";
        private const string EnableBMI1 = "COMPlus_EnableBMI1";
        private const string EnableBMI2 = "COMPlus_EnableBMI2";
        private const string EnableFMA = "COMPlus_EnableFMA";
        private const string EnableHWIntrinsic = "COMPlus_EnableHWIntrinsic";
        private const string EnableLZCNT = "COMPlus_EnableLZCNT";
        private const string EnablePCLMULQDQ = "COMPlus_EnablePCLMULQDQ";
        private const string EnablePOPCNT = "COMPlus_EnablePOPCNT";
        private const string EnableSSE = "COMPlus_EnableSSE";
        private const string EnableSSE2 = "COMPlus_EnableSSE2";
        private const string EnableSSE3 = "COMPlus_EnableSSE3";
        private const string EnableSSE3_4 = "COMPlus_EnableSSE3_4";
        private const string EnableSSE41 = "COMPlus_EnableSSE41";
        private const string EnableSSE42 = "COMPlus_EnableSSE42";
        private const string EnableSSSE3 = "COMPlus_EnableSSSE3";
        private const string FeatureSIMD = "COMPlus_FeatureSIMD";

        public class HwIntrinsics_SSE_AVX : Config
        {
            public HwIntrinsics_SSE_AVX()
            {
                this.AddJob(Job.Default.WithRuntime(CoreRuntime.Core31)
                    .WithEnvironmentVariables(
                        new EnvironmentVariable(EnableHWIntrinsic, Off),
                        new EnvironmentVariable(FeatureSIMD, Off))
                    .WithId("1. No HwIntrinsics").AsBaseline());

#if SUPPORTS_RUNTIME_INTRINSICS
                if (Avx.IsSupported)
                {
                    this.AddJob(Job.Default.WithRuntime(CoreRuntime.Core31)
                        .WithId("2. AVX"));
                }

                if (Sse.IsSupported)
                {
                    this.AddJob(Job.Default.WithRuntime(CoreRuntime.Core31)
                        .WithEnvironmentVariables(new EnvironmentVariable(EnableAVX, Off))
                        .WithId("3. SSE"));
                }
#endif
            }
        }
    }
}
