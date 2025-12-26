// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Numerics;
using System.Runtime.Intrinsics.Arm;
using System.Runtime.Intrinsics.X86;
using Xunit.Abstractions;
using Aes = System.Runtime.Intrinsics.X86.Aes;

namespace SixLabors.ImageSharp.Tests.TestUtilities.Tests;

public class FeatureTestRunnerTests
{
    public static TheoryData<HwIntrinsics, string[]> Intrinsics =>
        new()
        {
            { HwIntrinsics.DisableAES | HwIntrinsics.AllowAll, ["EnableAES", "AllowAll"] },
            { HwIntrinsics.DisableHWIntrinsic, ["EnableHWIntrinsic"] },
            { HwIntrinsics.DisableSSE42 | HwIntrinsics.DisableAVX, ["EnableSSE42", "EnableAVX"] }
        };

    [Theory]
    [MemberData(nameof(Intrinsics))]
    public void ToFeatureCollectionReturnsExpectedResult(HwIntrinsics expectedIntrinsics, string[] expectedValues)
    {
        Dictionary<HwIntrinsics, string> features = expectedIntrinsics.ToFeatureKeyValueCollection();
        HwIntrinsics[] keys = features.Keys.ToArray();

        HwIntrinsics actualIntrinsics = keys[0];
        for (int i = 1; i < keys.Length; i++)
        {
            actualIntrinsics |= keys[i];
        }

        Assert.Equal(expectedIntrinsics, actualIntrinsics);

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
            () => Assert.True(Vector.IsHardwareAccelerated, "Vector hardware acceleration should be enabled when AllowAll is specified."),
            HwIntrinsics.AllowAll);
    }

    [Fact]
    public void CanLimitHwIntrinsicBaseFeatures()
    {
        static void AssertDisabled()
        {
            Assert.False(Sse.IsSupported, "SSE should be disabled when DisableHWIntrinsic is set.");
            Assert.False(Sse2.IsSupported, "SSE2 should be disabled when DisableHWIntrinsic is set.");
            Assert.False(Aes.IsSupported, "AES (x86) should be disabled when DisableHWIntrinsic is set.");
            Assert.False(Pclmulqdq.IsSupported, "PCLMULQDQ should be disabled when DisableHWIntrinsic is set.");
            Assert.False(Sse3.IsSupported, "SSE3 should be disabled when DisableHWIntrinsic is set.");
            Assert.False(Ssse3.IsSupported, "SSSE3 should be disabled when DisableHWIntrinsic is set.");
            Assert.False(Sse41.IsSupported, "SSE4.1 should be disabled when DisableHWIntrinsic is set.");
            Assert.False(Sse42.IsSupported, "SSE4.2 should be disabled when DisableHWIntrinsic is set.");
            Assert.False(Popcnt.IsSupported, "POPCNT should be disabled when DisableHWIntrinsic is set.");
            Assert.False(Avx.IsSupported, "AVX should be disabled when DisableHWIntrinsic is set.");
            Assert.False(Fma.IsSupported, "FMA should be disabled when DisableHWIntrinsic is set.");
            Assert.False(Avx2.IsSupported, "AVX2 should be disabled when DisableHWIntrinsic is set.");
            Assert.False(Bmi1.IsSupported, "BMI1 should be disabled when DisableHWIntrinsic is set.");
            Assert.False(Bmi2.IsSupported, "BMI2 should be disabled when DisableHWIntrinsic is set.");
            Assert.False(Lzcnt.IsSupported, "LZCNT should be disabled when DisableHWIntrinsic is set.");
        }

        FeatureTestRunner.RunWithHwIntrinsicsFeature(
            AssertDisabled,
            HwIntrinsics.DisableHWIntrinsic);
    }

    [Fact]
    public void CanLimitHwIntrinsicFeaturesWithIntrinsicsParam()
    {
        static void AssertHwIntrinsicsFeatureDisabled(string intrinsic)
        {
            Assert.NotNull(intrinsic);

            switch (Enum.Parse<HwIntrinsics>(intrinsic))
            {
                case HwIntrinsics.DisableHWIntrinsic:
                    Assert.False(Sse.IsSupported, "SSE should be disabled when DisableHWIntrinsic is set.");
                    Assert.False(Sse2.IsSupported, "SSE2 should be disabled when DisableHWIntrinsic is set.");
                    Assert.False(Aes.IsSupported, "AES (x86) should be disabled when DisableHWIntrinsic is set.");
                    Assert.False(Pclmulqdq.IsSupported, "PCLMULQDQ should be disabled when DisableHWIntrinsic is set.");
                    Assert.False(Sse3.IsSupported, "SSE3 should be disabled when DisableHWIntrinsic is set.");
                    Assert.False(Ssse3.IsSupported, "SSSE3 should be disabled when DisableHWIntrinsic is set.");
                    Assert.False(Sse41.IsSupported, "SSE4.1 should be disabled when DisableHWIntrinsic is set.");
                    Assert.False(Sse42.IsSupported, "SSE4.2 should be disabled when DisableHWIntrinsic is set.");
                    Assert.False(Popcnt.IsSupported, "POPCNT should be disabled when DisableHWIntrinsic is set.");
                    Assert.False(Avx.IsSupported, "AVX should be disabled when DisableHWIntrinsic is set.");
                    Assert.False(Fma.IsSupported, "FMA should be disabled when DisableHWIntrinsic is set.");
                    Assert.False(Avx2.IsSupported, "AVX2 should be disabled when DisableHWIntrinsic is set.");
                    Assert.False(Bmi1.IsSupported, "BMI1 should be disabled when DisableHWIntrinsic is set.");
                    Assert.False(Bmi2.IsSupported, "BMI2 should be disabled when DisableHWIntrinsic is set.");
                    Assert.False(Lzcnt.IsSupported, "LZCNT should be disabled when DisableHWIntrinsic is set.");
                    Assert.False(AdvSimd.IsSupported, "Arm64 AdvSimd should be disabled when DisableHWIntrinsic is set.");
                    Assert.False(System.Runtime.Intrinsics.Arm.Aes.IsSupported, "Arm64 AES should be disabled when DisableHWIntrinsic is set.");
                    Assert.False(Crc32.IsSupported, "Arm64 CRC32 should be disabled when DisableHWIntrinsic is set.");
                    Assert.False(Dp.IsSupported, "Arm64 DotProd (DP) should be disabled when DisableHWIntrinsic is set.");
                    Assert.False(Sha1.IsSupported, "Arm64 SHA1 should be disabled when DisableHWIntrinsic is set.");
                    Assert.False(Sha256.IsSupported, "Arm64 SHA256 should be disabled when DisableHWIntrinsic is set.");
                    break;
                case HwIntrinsics.DisableAES:
                    Assert.False(Aes.IsSupported, "AES (x86) should be disabled when DisableAES is set.");
#if NET10_0_OR_GREATER
                    Assert.False(Pclmulqdq.IsSupported, "PCLMULQDQ should be disabled when DisableAES is set (paired disable).");
#endif
                    break;
                case HwIntrinsics.DisableSSE42:
#if NET10_0_OR_GREATER
                    Assert.False(Sse3.IsSupported, "Sse3 should be disabled.");
                    Assert.False(Ssse3.IsSupported, "Ssse3 should be disabled.");
                    Assert.False(Sse41.IsSupported, "Sse41 should be disabled.");
                    Assert.False(Popcnt.IsSupported, "Popcnt should be disabled.");
#else
                    Assert.False(Sse42.IsSupported, "Sse42 should be disabled when DisableSSE42 is set.");
#endif
                    break;
                case HwIntrinsics.DisableAVX:
                    Assert.False(Avx.IsSupported, "AVX should be disabled when DisableAVX is set.");
                    break;
                case HwIntrinsics.DisableAVX2:
                    Assert.False(Avx2.IsSupported, "AVX2 should be disabled when DisableAVX2 is set.");
#if NET10_0_OR_GREATER
                    Assert.False(Fma.IsSupported, "FMA should be disabled when DisableAVX2 is set (paired disable).");
                    Assert.False(Bmi1.IsSupported, "BMI1 should be disabled when DisableAVX2 is set (paired disable).");
                    Assert.False(Bmi2.IsSupported, "BMI2 should be disabled when DisableAVX2 is set (paired disable).");
                    Assert.False(Lzcnt.IsSupported, "LZCNT should be disabled when DisableAVX2 is set (paired disable).");
#endif
                    break;
                case HwIntrinsics.DisableArm64Aes:
                    Assert.False(System.Runtime.Intrinsics.Arm.Aes.IsSupported, "Arm64 AES should be disabled when DisableArm64Aes is set.");
                    break;
                case HwIntrinsics.DisableArm64Crc32:
                    Assert.False(Crc32.IsSupported, "Arm64 CRC32 should be disabled when DisableArm64Crc32 is set.");
                    break;
                case HwIntrinsics.DisableArm64Dp:
                    Assert.False(Dp.IsSupported, "Arm64 DotProd (DP) should be disabled when DisableArm64Dp is set.");
                    break;
                case HwIntrinsics.DisableArm64Sha1:
                    Assert.False(Sha1.IsSupported, "Arm64 SHA1 should be disabled when DisableArm64Sha1 is set.");
                    break;
                case HwIntrinsics.DisableArm64Sha256:
                    Assert.False(Sha256.IsSupported, "Arm64 SHA256 should be disabled when DisableArm64Sha256 is set.");
                    break;
            }
        }

        foreach (HwIntrinsics intrinsic in Enum.GetValues<HwIntrinsics>())
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
            Assert.False(Sse42.IsSupported, "SSE42 should be disabled when DisableSSE42 is set (sanity check using serializable param overload).");
        }

        FeatureTestRunner.RunWithHwIntrinsicsFeature(
            AssertHwIntrinsicsFeatureDisabled,
            HwIntrinsics.DisableSSE42,
            new FakeSerializable());
    }

    [Fact]
    public void CanLimitHwIntrinsicFeaturesWithSerializableAndIntrinsicsParams()
    {
        static void AssertHwIntrinsicsFeatureDisabled(string serializable, string intrinsic)
        {
            Assert.NotNull(serializable);
            Assert.NotNull(FeatureTestRunner.DeserializeForXunit<FakeSerializable>(serializable));

            switch (Enum.Parse<HwIntrinsics>(intrinsic))
            {
                case HwIntrinsics.DisableHWIntrinsic:
                    Assert.False(Sse.IsSupported, "SSE should be disabled when DisableHWIntrinsic is set.");
                    Assert.False(Sse2.IsSupported, "SSE2 should be disabled when DisableHWIntrinsic is set.");
                    Assert.False(Aes.IsSupported, "AES (x86) should be disabled when DisableHWIntrinsic is set.");
                    Assert.False(Pclmulqdq.IsSupported, "PCLMULQDQ should be disabled when DisableHWIntrinsic is set.");
                    Assert.False(Sse3.IsSupported, "SSE3 should be disabled when DisableHWIntrinsic is set.");
                    Assert.False(Ssse3.IsSupported, "SSSE3 should be disabled when DisableHWIntrinsic is set.");
                    Assert.False(Sse41.IsSupported, "SSE4.1 should be disabled when DisableHWIntrinsic is set.");
                    Assert.False(Sse42.IsSupported, "SSE4.2 should be disabled when DisableHWIntrinsic is set.");
                    Assert.False(Popcnt.IsSupported, "POPCNT should be disabled when DisableHWIntrinsic is set.");
                    Assert.False(Avx.IsSupported, "AVX should be disabled when DisableHWIntrinsic is set.");
                    Assert.False(Fma.IsSupported, "FMA should be disabled when DisableHWIntrinsic is set.");
                    Assert.False(Avx2.IsSupported, "AVX2 should be disabled when DisableHWIntrinsic is set.");
                    Assert.False(Bmi1.IsSupported, "BMI1 should be disabled when DisableHWIntrinsic is set.");
                    Assert.False(Bmi2.IsSupported, "BMI2 should be disabled when DisableHWIntrinsic is set.");
                    Assert.False(Lzcnt.IsSupported, "LZCNT should be disabled when DisableHWIntrinsic is set.");
                    Assert.False(AdvSimd.IsSupported, "Arm64 AdvSimd should be disabled when DisableHWIntrinsic is set.");
                    Assert.False(System.Runtime.Intrinsics.Arm.Aes.IsSupported, "Arm64 AES should be disabled when DisableHWIntrinsic is set.");
                    Assert.False(Crc32.IsSupported, "Arm64 CRC32 should be disabled when DisableHWIntrinsic is set.");
                    Assert.False(Dp.IsSupported, "Arm64 DotProd (DP) should be disabled when DisableHWIntrinsic is set.");
                    Assert.False(Sha1.IsSupported, "Arm64 SHA1 should be disabled when DisableHWIntrinsic is set.");
                    Assert.False(Sha256.IsSupported, "Arm64 SHA256 should be disabled when DisableHWIntrinsic is set.");
                    break;
                case HwIntrinsics.DisableAES:
                    Assert.False(Aes.IsSupported, "AES (x86) should be disabled when DisableAES is set.");
#if NET10_0_OR_GREATER
                    Assert.False(Pclmulqdq.IsSupported, "PCLMULQDQ should be disabled when DisableAES is set (paired disable).");
#endif
                    break;
                case HwIntrinsics.DisableSSE42:
#if NET10_0_OR_GREATER
                    Assert.False(Sse3.IsSupported, "Sse3 should be disabled.");
                    Assert.False(Ssse3.IsSupported, "Ssse3 should be disabled.");
                    Assert.False(Sse41.IsSupported, "Sse41 should be disabled.");
                    Assert.False(Popcnt.IsSupported, "Popcnt should be disabled.");
#endif
                    Assert.False(Sse42.IsSupported, "Sse42 should be disabled when DisableSSE42 is set.");
                    break;
                case HwIntrinsics.DisableAVX:
                    Assert.False(Avx.IsSupported, "AVX should be disabled when DisableAVX is set.");
                    break;
                case HwIntrinsics.DisableAVX2:
                    Assert.False(Avx2.IsSupported, "AVX2 should be disabled when DisableAVX2 is set.");
#if NET10_0_OR_GREATER
                    Assert.False(Fma.IsSupported, "FMA should be disabled when DisableAVX2 is set (paired disable).");
                    Assert.False(Bmi1.IsSupported, "BMI1 should be disabled when DisableAVX2 is set (paired disable).");
                    Assert.False(Bmi2.IsSupported, "BMI2 should be disabled when DisableAVX2 is set (paired disable).");
                    Assert.False(Lzcnt.IsSupported, "LZCNT should be disabled when DisableAVX2 is set (paired disable).");
#endif
                    break;
                case HwIntrinsics.DisableArm64Aes:
                    Assert.False(System.Runtime.Intrinsics.Arm.Aes.IsSupported, "Arm64 AES should be disabled when DisableArm64Aes is set.");
                    break;
                case HwIntrinsics.DisableArm64Crc32:
                    Assert.False(Crc32.IsSupported, "Arm64 CRC32 should be disabled when DisableArm64Crc32 is set.");
                    break;
                case HwIntrinsics.DisableArm64Dp:
                    Assert.False(Dp.IsSupported, "Arm64 DotProd (DP) should be disabled when DisableArm64Dp is set.");
                    break;
                case HwIntrinsics.DisableArm64Sha1:
                    Assert.False(Sha1.IsSupported, "Arm64 SHA1 should be disabled when DisableArm64Sha1 is set.");
                    break;
                case HwIntrinsics.DisableArm64Sha256:
                    Assert.False(Sha256.IsSupported, "Arm64 SHA256 should be disabled when DisableArm64Sha256 is set.");
                    break;
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
