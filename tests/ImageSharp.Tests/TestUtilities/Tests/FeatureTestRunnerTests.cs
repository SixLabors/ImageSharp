// Copyright (c) Six Labors.
// Licensed under the Apache License, Version 2.0.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
#if SUPPORTS_RUNTIME_INTRINSICS
using System.Runtime.Intrinsics.X86;
#endif
using Xunit;
using Xunit.Abstractions;

namespace SixLabors.ImageSharp.Tests.TestUtilities.Tests
{
    public class FeatureTestRunnerTests
    {
        public static TheoryData<HwIntrinsics, string[]> Intrinsics =>
            new TheoryData<HwIntrinsics, string[]>
            {
                { HwIntrinsics.DisableAES | HwIntrinsics.AllowAll, new string[] { "EnableAES", "AllowAll" } },
                { HwIntrinsics.DisableSIMD | HwIntrinsics.DisableHWIntrinsic, new string[] { "FeatureSIMD", "EnableHWIntrinsic" } },
                { HwIntrinsics.DisableSSE42 | HwIntrinsics.DisableAVX, new string[] { "EnableSSE42", "EnableAVX" } }
            };

        [Theory]
        [MemberData(nameof(Intrinsics))]
        public void ToFeatureCollectionReturnsExpectedResult(HwIntrinsics expectedItrinsics, string[] expectedValues)
        {
            Dictionary<HwIntrinsics, string> features = expectedItrinsics.ToFeatureKeyValueCollection();
            HwIntrinsics[] keys = features.Keys.ToArray();

            HwIntrinsics actualIntrinsics = keys[0];
            for (int i = 1; i < keys.Length; i++)
            {
                actualIntrinsics |= keys[i];
            }

            Assert.Equal(expectedItrinsics, actualIntrinsics);

            IEnumerable<string> actualValues = features.Select(x => x.Value);
            Assert.Equal(expectedValues, actualValues);
        }

        [Fact]
        public void AllowsAllHwIntrinsicFeatures()
        {
            if (!Vector.IsHardwareAccelerated)
            {
                return;
            }

            FeatureTestRunner.RunWithHwIntrinsicsFeature(
                () => Assert.True(Vector.IsHardwareAccelerated),
                HwIntrinsics.AllowAll);
        }

        [Fact]
        public void CanLimitHwIntrinsicSIMDFeatures()
        {
            FeatureTestRunner.RunWithHwIntrinsicsFeature(
                () => Assert.False(Vector.IsHardwareAccelerated),
                HwIntrinsics.DisableSIMD);
        }

#if SUPPORTS_RUNTIME_INTRINSICS
        [Fact]
        public void CanLimitHwIntrinsicBaseFeatures()
        {
            static void AssertDisabled()
            {
                Assert.False(Sse.IsSupported);
                Assert.False(Sse2.IsSupported);
                Assert.False(Aes.IsSupported);
                Assert.False(Pclmulqdq.IsSupported);
                Assert.False(Sse3.IsSupported);
                Assert.False(Ssse3.IsSupported);
                Assert.False(Sse41.IsSupported);
                Assert.False(Sse42.IsSupported);
                Assert.False(Popcnt.IsSupported);
                Assert.False(Avx.IsSupported);
                Assert.False(Fma.IsSupported);
                Assert.False(Avx2.IsSupported);
                Assert.False(Bmi1.IsSupported);
                Assert.False(Bmi2.IsSupported);
                Assert.False(Lzcnt.IsSupported);
            }

            FeatureTestRunner.RunWithHwIntrinsicsFeature(
                AssertDisabled,
                HwIntrinsics.DisableHWIntrinsic);
        }
#endif

        [Fact]
        public void CanLimitHwIntrinsicFeaturesWithIntrinsicsParam()
        {
            static void AssertHwIntrinsicsFeatureDisabled(string intrinsic)
            {
                Assert.NotNull(intrinsic);

                switch ((HwIntrinsics)Enum.Parse(typeof(HwIntrinsics), intrinsic))
                {
                    case HwIntrinsics.DisableSIMD:
                        Assert.False(Vector.IsHardwareAccelerated);
                        break;
#if SUPPORTS_RUNTIME_INTRINSICS
                    case HwIntrinsics.DisableHWIntrinsic:
                        Assert.False(Sse.IsSupported);
                        Assert.False(Sse2.IsSupported);
                        Assert.False(Aes.IsSupported);
                        Assert.False(Pclmulqdq.IsSupported);
                        Assert.False(Sse3.IsSupported);
                        Assert.False(Ssse3.IsSupported);
                        Assert.False(Sse41.IsSupported);
                        Assert.False(Sse42.IsSupported);
                        Assert.False(Popcnt.IsSupported);
                        Assert.False(Avx.IsSupported);
                        Assert.False(Fma.IsSupported);
                        Assert.False(Avx2.IsSupported);
                        Assert.False(Bmi1.IsSupported);
                        Assert.False(Bmi2.IsSupported);
                        Assert.False(Lzcnt.IsSupported);
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

            foreach (HwIntrinsics intrinsic in (HwIntrinsics[])Enum.GetValues(typeof(HwIntrinsics)))
            {
                FeatureTestRunner.RunWithHwIntrinsicsFeature(AssertHwIntrinsicsFeatureDisabled, intrinsic);
            }
        }

        [Fact]
        public void CanLimitHwIntrinsicFeaturesWithSerializableParam()
        {
            static void AssertHwIntrinsicsFeatureDisabled(string serializable)
            {
                Assert.NotNull(serializable);
                Assert.NotNull(FeatureTestRunner.DeserializeForXunit<FakeSerializable>(serializable));

#if SUPPORTS_RUNTIME_INTRINSICS
                Assert.False(Sse.IsSupported);
#endif
            }

            FeatureTestRunner.RunWithHwIntrinsicsFeature(
                AssertHwIntrinsicsFeatureDisabled,
                HwIntrinsics.DisableSSE,
                new FakeSerializable());
        }

        [Fact]
        public void CanLimitHwIntrinsicFeaturesWithSerializableAndIntrinsicsParams()
        {
            static void AssertHwIntrinsicsFeatureDisabled(string serializable, string intrinsic)
            {
                Assert.NotNull(serializable);
                Assert.NotNull(FeatureTestRunner.DeserializeForXunit<FakeSerializable>(serializable));

                switch ((HwIntrinsics)Enum.Parse(typeof(HwIntrinsics), intrinsic))
                {
                    case HwIntrinsics.DisableSIMD:
                        Assert.False(Vector.IsHardwareAccelerated, nameof(Vector.IsHardwareAccelerated));
                        break;
#if SUPPORTS_RUNTIME_INTRINSICS
                    case HwIntrinsics.DisableHWIntrinsic:
                        Assert.False(Sse.IsSupported);
                        Assert.False(Sse2.IsSupported);
                        Assert.False(Aes.IsSupported);
                        Assert.False(Pclmulqdq.IsSupported);
                        Assert.False(Sse3.IsSupported);
                        Assert.False(Ssse3.IsSupported);
                        Assert.False(Sse41.IsSupported);
                        Assert.False(Sse42.IsSupported);
                        Assert.False(Popcnt.IsSupported);
                        Assert.False(Avx.IsSupported);
                        Assert.False(Fma.IsSupported);
                        Assert.False(Avx2.IsSupported);
                        Assert.False(Bmi1.IsSupported);
                        Assert.False(Bmi2.IsSupported);
                        Assert.False(Lzcnt.IsSupported);
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

            foreach (HwIntrinsics intrinsic in (HwIntrinsics[])Enum.GetValues(typeof(HwIntrinsics)))
            {
                FeatureTestRunner.RunWithHwIntrinsicsFeature(AssertHwIntrinsicsFeatureDisabled, intrinsic, new FakeSerializable());
            }
        }

        public class FakeSerializable : IXunitSerializable
        {
            public void Deserialize(IXunitSerializationInfo info)
            {
            }

            public void Serialize(IXunitSerializationInfo info)
            {
            }
        }
    }
}
