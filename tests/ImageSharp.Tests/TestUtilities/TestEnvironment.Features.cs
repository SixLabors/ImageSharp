// Copyright (c) Six Labors.
// Licensed under the Apache License, Version 2.0.

namespace SixLabors.ImageSharp.Tests
{
    public static partial class TestEnvironment
    {
        internal static class Features
        {
            public const string On = "1";
            public const string Off = "0";

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
            public const string EnableAES = "COMPlus_EnableAES";
            public const string EnableAVX = "COMPlus_EnableAVX";
            public const string EnableAVX2 = "COMPlus_EnableAVX2";
            public const string EnableBMI1 = "COMPlus_EnableBMI1";
            public const string EnableBMI2 = "COMPlus_EnableBMI2";
            public const string EnableFMA = "COMPlus_EnableFMA";
            public const string EnableHWIntrinsic = "COMPlus_EnableHWIntrinsic";
            public const string EnableLZCNT = "COMPlus_EnableLZCNT";
            public const string EnablePCLMULQDQ = "COMPlus_EnablePCLMULQDQ";
            public const string EnablePOPCNT = "COMPlus_EnablePOPCNT";
            public const string EnableSSE = "COMPlus_EnableSSE";
            public const string EnableSSE2 = "COMPlus_EnableSSE2";
            public const string EnableSSE3 = "COMPlus_EnableSSE3";
            public const string EnableSSE3_4 = "COMPlus_EnableSSE3_4";
            public const string EnableSSE41 = "COMPlus_EnableSSE41";
            public const string EnableSSE42 = "COMPlus_EnableSSE42";
            public const string EnableSSSE3 = "COMPlus_EnableSSSE3";
            public const string FeatureSIMD = "COMPlus_FeatureSIMD";
        }
    }
}
