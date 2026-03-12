// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Runtime.Intrinsics.X86;
using BenchmarkDotNet.Environments;
using BenchmarkDotNet.Jobs;

namespace SixLabors.ImageSharp.Benchmarks;

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
    private const string EnableAES = "DOTNET_EnableAES";
    private const string EnableAVX512F = "DOTNET_EnableAVX512F";
    private const string EnableAVX = "DOTNET_EnableAVX";
    private const string EnableAVX2 = "DOTNET_EnableAVX2";
    private const string EnableBMI1 = "DOTNET_EnableBMI1";
    private const string EnableBMI2 = "DOTNET_EnableBMI2";
    private const string EnableFMA = "DOTNET_EnableFMA";
    private const string EnableHWIntrinsic = "DOTNET_EnableHWIntrinsic";
    private const string EnableLZCNT = "DOTNET_EnableLZCNT";
    private const string EnablePCLMULQDQ = "DOTNET_EnablePCLMULQDQ";
    private const string EnablePOPCNT = "DOTNET_EnablePOPCNT";
    private const string EnableSSE = "DOTNET_EnableSSE";
    private const string EnableSSE2 = "DOTNET_EnableSSE2";
    private const string EnableSSE3 = "DOTNET_EnableSSE3";
    private const string EnableSSE3_4 = "DOTNET_EnableSSE3_4";
    private const string EnableSSE41 = "DOTNET_EnableSSE41";
    private const string EnableSSE42 = "DOTNET_EnableSSE42";
    private const string EnableSSSE3 = "DOTNET_EnableSSSE3";
    private const string FeatureSIMD = "DOTNET_FeatureSIMD";

    public class HwIntrinsics_SSE_AVX : Config
    {
        public HwIntrinsics_SSE_AVX()
        {
            this.AddJob(Job.Default.WithRuntime(CoreRuntime.Core80)
                .WithEnvironmentVariables(
                    new EnvironmentVariable(EnableHWIntrinsic, Off),
                    new EnvironmentVariable(FeatureSIMD, Off))
                .WithId("1. No HwIntrinsics").AsBaseline());

            if (Sse.IsSupported)
            {
                this.AddJob(Job.Default.WithRuntime(CoreRuntime.Core80)
                    .WithEnvironmentVariables(new EnvironmentVariable(EnableAVX, Off))
                    .WithId("2. SSE"));
            }

            if (Avx.IsSupported)
            {
                this.AddJob(Job.Default.WithRuntime(CoreRuntime.Core80)
                    .WithId("3. AVX"));
            }
        }
    }

    public class HwIntrinsics_SSE_AVX_AVX512F : Config
    {
        public HwIntrinsics_SSE_AVX_AVX512F()
        {
            this.AddJob(Job.Default.WithRuntime(CoreRuntime.Core80)
                .WithEnvironmentVariables(
                    new EnvironmentVariable(EnableHWIntrinsic, Off),
                    new EnvironmentVariable(FeatureSIMD, Off))
                .WithId("1. No HwIntrinsics").AsBaseline());

            if (Sse.IsSupported)
            {
                this.AddJob(Job.Default.WithRuntime(CoreRuntime.Core80)
                    .WithEnvironmentVariables(new EnvironmentVariable(EnableAVX, Off))
                    .WithId("2. SSE"));
            }

            if (Avx.IsSupported)
            {
                this.AddJob(Job.Default.WithRuntime(CoreRuntime.Core80)
                    .WithEnvironmentVariables(new EnvironmentVariable(EnableAVX512F, Off))
                    .WithId("3. AVX"));
            }

            if (Avx512F.IsSupported)
            {
                this.AddJob(Job.Default.WithRuntime(CoreRuntime.Core80)
                    .WithId("3. AVX512F"));
            }
        }
    }
}
