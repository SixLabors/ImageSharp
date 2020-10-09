// Copyright (c) Six Labors.
// Licensed under the Apache License, Version 2.0.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Numerics;
#if SUPPORTS_RUNTIME_INTRINSICS
using System.Runtime.Intrinsics.X86;
#endif
using Microsoft.DotNet.RemoteExecutor;
using Xunit;
using Xunit.Abstractions;

namespace SixLabors.ImageSharp.Tests.TestUtilities
{
    /// <summary>
    /// Allows the testing against specific feature sets.
    /// </summary>
    public static class FeatureTestRunner
    {
        private static readonly char[] SplitChars = new[] { ',', ' ' };

        /// <summary>
        /// Allows the deserialization of parameters passed to the feature test.
        /// <remark>
        /// <para>
        /// This is required because <see cref="RemoteExecutor"/> does not allow
        /// marshalling of fields so we cannot pass a wrapped <see cref="Action{T}"/>
        /// allowing automatic deserialization.
        /// </para>
        /// </remark>
        /// </summary>
        /// <typeparam name="T">The type to deserialize to.</typeparam>
        /// <param name="value">The string value to deserialize.</param>
        /// <returns>The <see cref="T"/> value.</returns>
        public static T Deserialize<T>(string value)
            where T : IXunitSerializable
            => BasicSerializer.Deserialize<T>(value);

        // TODO: Write runner test and use this.
        private static void AssertHwIntrinsicsFeatureDisabled(HwIntrinsics intrinsics)
        {
            switch (intrinsics)
            {
                case HwIntrinsics.DisableSIMD:
                    Assert.False(Vector.IsHardwareAccelerated);
                    break;
#if SUPPORTS_RUNTIME_INTRINSICS
                case HwIntrinsics.DisableHWIntrinsic:
                    Assert.False(Vector.IsHardwareAccelerated);
                    break;
                case HwIntrinsics.DisableSSE:
                    Assert.False(Sse.IsSupported);
                    break;
                case HwIntrinsics.DisableSSE2:
                    Assert.False(Sse2.IsSupported);
                    break;
                case HwIntrinsics.DisableAES:
                    Assert.False(Aes.IsSupported);
                    break;
                case HwIntrinsics.DisablePCLMULQDQ:
                    Assert.False(Pclmulqdq.IsSupported);
                    break;
                case HwIntrinsics.DisableSSE3:
                    Assert.False(Sse3.IsSupported);
                    break;
                case HwIntrinsics.DisableSSSE3:
                    Assert.False(Ssse3.IsSupported);
                    break;
                case HwIntrinsics.DisableSSE41:
                    Assert.False(Sse41.IsSupported);
                    break;
                case HwIntrinsics.DisableSSE42:
                    Assert.False(Sse42.IsSupported);
                    break;
                case HwIntrinsics.DisablePOPCNT:
                    Assert.False(Popcnt.IsSupported);
                    break;
                case HwIntrinsics.DisableAVX:
                    Assert.False(Avx.IsSupported);
                    break;
                case HwIntrinsics.DisableFMA:
                    Assert.False(Fma.IsSupported);
                    break;
                case HwIntrinsics.DisableAVX2:
                    Assert.False(Avx2.IsSupported);
                    break;
                case HwIntrinsics.DisableBMI1:
                    Assert.False(Bmi1.IsSupported);
                    break;
                case HwIntrinsics.DisableBMI2:
                    Assert.False(Bmi2.IsSupported);
                    break;
                case HwIntrinsics.DisableLZCNT:
                    Assert.False(Lzcnt.IsSupported);
                    break;
#endif
            }
        }

        /// <summary>
        /// Runs the given test <paramref name="action"/> within an environment
        /// where the given <paramref name="intrinsics"/> features.
        /// </summary>
        /// <param name="action">The test action to run.</param>
        /// <param name="intrinsics">The intrinsics features.</param>
        public static void RunWithHwIntrinsicsFeature(
            Action action,
            HwIntrinsics intrinsics)
        {
            if (!RemoteExecutor.IsSupported)
            {
                return;
            }

            foreach (string intrinsic in intrinsics.ToFeatureCollection())
            {
                var processStartInfo = new ProcessStartInfo();
                if (intrinsic != nameof(HwIntrinsics.AllowAll))
                {
                    processStartInfo.Environment[$"COMPlus_{intrinsic}"] = "0";
                }

                RemoteExecutor.Invoke(
                    action,
                    new RemoteInvokeOptions
                    {
                        StartInfo = processStartInfo
                    })
                    .Dispose();
            }
        }

        /// <summary>
        /// Runs the given test <paramref name="action"/> within an environment
        /// where the given <paramref name="intrinsics"/> features.
        /// </summary>
        /// <param name="action">The test action to run.</param>
        /// <param name="intrinsics">The intrinsics features.</param>
        /// <param name="serializable">The value to pass as a parameter to the test action.</param>
        public static void RunWithHwIntrinsicsFeature<T>(
            Action<string> action,
            HwIntrinsics intrinsics,
            T serializable)
            where T : IXunitSerializable
        {
            if (!RemoteExecutor.IsSupported)
            {
                return;
            }

            foreach (string intrinsic in intrinsics.ToFeatureCollection())
            {
                var processStartInfo = new ProcessStartInfo();
                if (intrinsic != nameof(HwIntrinsics.AllowAll))
                {
                    processStartInfo.Environment[$"COMPlus_Enable{intrinsic}"] = "0";
                }

                RemoteExecutor.Invoke(
                    action,
                    BasicSerializer.Serialize(serializable),
                    new RemoteInvokeOptions
                    {
                        StartInfo = processStartInfo
                    })
                    .Dispose();
            }
        }

        private static IEnumerable<string> ToFeatureCollection(this HwIntrinsics intrinsics)
        {
            // Loop through and translate the given values into COMPlus equivaluents
            var features = new List<string>();
            var split = intrinsics.ToString("G").Split(SplitChars, StringSplitOptions.RemoveEmptyEntries).ToArray();
            foreach (string intrinsic in split)
            {
                switch (intrinsic)
                {
                    case nameof(HwIntrinsics.DisableSIMD):
                        features.Add("FeatureSIMD");
                        break;

                    case nameof(HwIntrinsics.AllowAll):

                        // Not a COMPlus value. We filter in calling method.
                        features.Add(nameof(HwIntrinsics.AllowAll));
                        break;

                    default:
                        features.Add(intrinsic.Replace("Disable", "Enable"));
                        break;
                }
            }

            return features;
        }
    }

    /// <summary>
    /// See <see href="https://github.com/dotnet/runtime/blob/50ac454d8d8a1915188b2a4bb3fff3b81bf6c0cf/src/coreclr/src/jit/jitconfigvalues.h#L224"/>
    /// <remarks>
    /// <see cref="DisableSIMD"/> ends up impacting all SIMD support(including System.Numerics)
    /// but not things like <see cref="DisableBMI1"/>, <see cref="DisableBMI2"/>, and <see cref="DisableLZCNT"/>.
    /// </remarks>
    /// </summary>
    [Flags]
    public enum HwIntrinsics
    {
        // Use flags so we can pass multiple values without using params.
        DisableSIMD = 0,
        DisableHWIntrinsic = 1 << 0,
        DisableSSE = 1 << 1,
        DisableSSE2 = 1 << 2,
        DisableAES = 1 << 3,
        DisablePCLMULQDQ = 1 << 4,
        DisableSSE3 = 1 << 5,
        DisableSSSE3 = 1 << 6,
        DisableSSE41 = 1 << 7,
        DisableSSE42 = 1 << 8,
        DisablePOPCNT = 1 << 9,
        DisableAVX = 1 << 10,
        DisableFMA = 1 << 11,
        DisableAVX2 = 1 << 12,
        DisableBMI1 = 1 << 13,
        DisableBMI2 = 1 << 14,
        DisableLZCNT = 1 << 15,
        AllowAll = 1 << 16
    }
}
