// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Numerics;
using System.Runtime.InteropServices;
using SixLabors.ImageSharp.ColorProfiles;
using SixLabors.ImageSharp.Tests.TestUtilities;

namespace SixLabors.ImageSharp.Tests.ColorProfiles;

/// <summary>
/// Tests the <see cref="YCbCr"/> struct.
/// </summary>
[Trait("Color", "Conversion")]
public class YCbCrTests
{
    private static readonly ApproximateColorProfileComparer Comparer = new(.0001F);

    [Fact]
    public void YCbCrConstructorAssignsFields()
    {
        const float y = .75F;
        const float cb = .64F;
        const float cr = .87F;
        YCbCr yCbCr = new(y, cb, cr);

        Assert.Equal(y, yCbCr.Y);
        Assert.Equal(cb, yCbCr.Cb);
        Assert.Equal(cr, yCbCr.Cr);
    }

    [Fact]
    public void YCbCrEquality()
    {
        YCbCr x = default;
        YCbCr y = new(Vector3.One);

        Assert.True(default == default(YCbCr));
        Assert.False(default != default(YCbCr));
        Assert.Equal(default, default(YCbCr));
        Assert.Equal(new YCbCr(1, 0, 1), new YCbCr(1, 0, 1));
        Assert.Equal(new YCbCr(Vector3.One), new YCbCr(Vector3.One));
        Assert.False(x.Equals(y));
        Assert.False(x.Equals((object)y));
        Assert.False(x.GetHashCode().Equals(y.GetHashCode()));
    }

    /// <summary>
    /// Verifies native YCbCr bulk conversion when the packed source and RGB destination share storage.
    /// </summary>
    [Theory]
    [InlineData(16)]
    [InlineData(31)]
    public void ToProfileConnectionSpace_MixedSamplesSupportInPlaceConversion(int count) => FeatureTestRunner.RunWithHwIntrinsicsFeature(RunMixedInPlaceConversion, count, HwIntrinsics.AllowAll | HwIntrinsics.DisableAVX512F | HwIntrinsics.DisableAVX | HwIntrinsics.DisableHWIntrinsic);

    /// <summary>
    /// Compares aliased YCbCr/RGB rows after disabled SIMD features are selected in separate processes before JIT compilation.
    /// </summary>
    /// <param name="serialized">The serialized row length.</param>
    private static void RunMixedInPlaceConversion(string serialized)
    {
        int count = FeatureTestRunner.Deserialize<int>(serialized);
        YCbCr[] source = new YCbCr[count];
        Rgb[] expected = new Rgb[count];
        YCbCrTransform baseTransform = KnownYCbCrMatrices.BT601;
        YCbCrTransform transform = new(baseTransform.Forward, baseTransform.Inverse, new Vector3(0F, 128F / 255F, 128F / 255F));
        ColorConversionOptions options = new() { YCbCrTransform = transform };

        for (int i = 0; i < count; i++)
        {
            // FromScaledVector4 preserves fractional and out-of-range components used by decoded JPEG samples.
            source[i] = YCbCr.FromScaledVector4(new Vector4((i * 7F) / 19F, ((i * 11F) / 19F) - .2F, ((i * 13F) / 19F) - .3F, 1F));
            expected[i] = source[i].ToProfileConnectingSpace(options);
        }

        Span<Rgb> destination = MemoryMarshal.Cast<YCbCr, Rgb>(source.AsSpan());
        YCbCr.ToProfileConnectionSpace(options, source, destination);

        for (int i = 0; i < count; i++)
        {
            Assert.Equal(expected[i], destination[i], Comparer);
        }
    }
}
