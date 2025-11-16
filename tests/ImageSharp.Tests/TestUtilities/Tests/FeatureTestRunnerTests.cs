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
            () => Assert.True(Vector.IsHardwareAccelerated),
            HwIntrinsics.AllowAll);
    }

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

    [Fact]
    public void CanLimitHwIntrinsicFeaturesWithIntrinsicsParam()
    {
        static void AssertHwIntrinsicsFeatureDisabled(string intrinsic)
        {
            Assert.NotNull(intrinsic);

            switch (Enum.Parse<HwIntrinsics>(intrinsic))
            {
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
                    Assert.False(AdvSimd.IsSupported);
                    Assert.False(System.Runtime.Intrinsics.Arm.Aes.IsSupported);
                    Assert.False(Crc32.IsSupported);
                    Assert.False(Dp.IsSupported);
                    Assert.False(Sha1.IsSupported);
                    Assert.False(Sha256.IsSupported);
                    break;
                case HwIntrinsics.DisableAES:
                    Assert.False(Aes.IsSupported);
                    Assert.False(Pclmulqdq.IsSupported);
                    break;
                case HwIntrinsics.DisableSSE42:
                    Assert.False(Sse2.IsSupported, "Sse2 should be disabled.");
                    Assert.False(Sse3.IsSupported, "Sse3 should be disabled.");
                    Assert.False(Ssse3.IsSupported, "Ssse3 should be disabled.");
                    Assert.False(Sse41.IsSupported, "Sse41 should be disabled.");
                    Assert.False(Sse42.IsSupported, "Sse42 should be disabled.");
                    Assert.False(Popcnt.IsSupported, "Popcnt should be disabled.");
                    break;
                case HwIntrinsics.DisableAVX:
                    Assert.False(Avx.IsSupported);
                    break;
                case HwIntrinsics.DisableAVX2:
                    Assert.False(Avx2.IsSupported);
                    Assert.False(Fma.IsSupported);
                    Assert.False(Bmi1.IsSupported);
                    Assert.False(Bmi2.IsSupported);
                    Assert.False(Lzcnt.IsSupported);
                    break;
                case HwIntrinsics.DisableArm64Aes:
                    Assert.False(System.Runtime.Intrinsics.Arm.Aes.IsSupported);
                    break;
                case HwIntrinsics.DisableArm64Crc32:
                    Assert.False(Crc32.IsSupported);
                    break;
                case HwIntrinsics.DisableArm64Dp:
                    Assert.False(Dp.IsSupported);
                    break;
                case HwIntrinsics.DisableArm64Sha1:
                    Assert.False(Sha1.IsSupported);
                    break;
                case HwIntrinsics.DisableArm64Sha256:
                    Assert.False(Sha256.IsSupported);
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
            Assert.False(Sse.IsSupported);
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
                    Assert.False(AdvSimd.IsSupported);
                    Assert.False(System.Runtime.Intrinsics.Arm.Aes.IsSupported);
                    Assert.False(Crc32.IsSupported);
                    Assert.False(Dp.IsSupported);
                    Assert.False(Sha1.IsSupported);
                    Assert.False(Sha256.IsSupported);
                    break;
                case HwIntrinsics.DisableAES:
                    Assert.False(Aes.IsSupported);
                    Assert.False(Pclmulqdq.IsSupported);
                    break;
                case HwIntrinsics.DisableSSE42:
                    Assert.False(Sse2.IsSupported);
                    Assert.False(Ssse3.IsSupported);
                    Assert.False(Sse41.IsSupported);
                    Assert.False(Sse42.IsSupported);
                    Assert.False(Popcnt.IsSupported);
                    break;
                case HwIntrinsics.DisableAVX:
                    Assert.False(Avx.IsSupported);
                    break;
                case HwIntrinsics.DisableAVX2:
                    Assert.False(Avx2.IsSupported);
                    Assert.False(Fma.IsSupported);
                    Assert.False(Bmi1.IsSupported);
                    Assert.False(Bmi2.IsSupported);
                    Assert.False(Lzcnt.IsSupported);
                    break;
                case HwIntrinsics.DisableArm64Aes:
                    Assert.False(System.Runtime.Intrinsics.Arm.Aes.IsSupported);
                    break;
                case HwIntrinsics.DisableArm64Crc32:
                    Assert.False(Crc32.IsSupported);
                    break;
                case HwIntrinsics.DisableArm64Dp:
                    Assert.False(Dp.IsSupported);
                    break;
                case HwIntrinsics.DisableArm64Sha1:
                    Assert.False(Sha1.IsSupported);
                    break;
                case HwIntrinsics.DisableArm64Sha256:
                    Assert.False(Sha256.IsSupported);
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
