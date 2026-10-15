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
    /// Verifies both bulk conversion directions when packed RGB and YCbCr share storage.
    /// </summary>
    [Theory]
    [InlineData(16)]
    [InlineData(31)]
    public void ProfileConnectionSpace_MixedSamplesSupportInPlaceConversion(int count)
        => FeatureTestRunner.RunWithHwIntrinsicsFeature(
            RunMixedInPlaceConversion,
            count,
            HwIntrinsics.AllowAll | HwIntrinsics.DisableAVX512F | HwIntrinsics.DisableAVX | HwIntrinsics.DisableHWIntrinsic);

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
        ReadOnlySpan<YCbCr> samples =
        [
            new(0F, .5F, .5F), new(1F, .5F, .5F), new(.25F, .75F, .1F), new(.8F, -.2F, .6F),
            new(.4F, 1.2F, .3F), new(.65F, .15F, 1.1F), new(.05F, .9F, .85F), new(.93F, .31F, -.1F),
            new(.37F, .68F, .24F), new(.72F, .02F, .97F), new(.14F, .82F, .43F)
        ];

        for (int i = 0; i < count; i++)
        {
            // Fractional and out-of-range values repeat every eleven pixels, crossing each SIMD group.
            source[i] = samples[i % samples.Length];
            expected[i] = source[i].ToProfileConnectingSpace(options);
        }

        Span<Rgb> destination = MemoryMarshal.Cast<YCbCr, Rgb>(source.AsSpan());
        YCbCr.ToProfileConnectionSpace(options, source, destination);

        YCbCr[] reverseExpected = new YCbCr[count];

        for (int i = 0; i < count; i++)
        {
            Assert.Equal(expected[i], destination[i], Comparer);
            reverseExpected[i] = YCbCr.FromProfileConnectingSpace(options, in destination[i]);
        }

        // Reverse conversion must also permit in-place use without overwriting unread RGB triples.
        YCbCr.FromProfileConnectionSpace(options, destination, source);

        for (int i = 0; i < count; i++)
        {
            Assert.Equal(reverseExpected[i], source[i], Comparer);
        }
    }
}
