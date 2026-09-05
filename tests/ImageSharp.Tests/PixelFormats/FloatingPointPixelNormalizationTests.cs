// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Numerics;
using SixLabors.ImageSharp.PixelFormats;

namespace SixLabors.ImageSharp.Tests.PixelFormats;

[Trait("Category", "PixelFormats")]
public class FloatingPointPixelNormalizationTests
{
    /// <summary>
    /// HalfSingle normalizes scaled input identically in scalar and bulk conversions.
    /// </summary>
    [Fact]
    public void HalfSingle_ScaledInputIsNormalized() => AssertScaledInputIsNormalized<HalfSingle>();

    /// <summary>
    /// HalfVector2 normalizes scaled input identically in scalar and bulk conversions.
    /// </summary>
    [Fact]
    public void HalfVector2_ScaledInputIsNormalized() => AssertScaledInputIsNormalized<HalfVector2>();

    /// <summary>
    /// HalfVector4 normalizes scaled input identically in scalar and bulk conversions.
    /// </summary>
    [Fact]
    public void HalfVector4_ScaledInputIsNormalized() => AssertScaledInputIsNormalized<HalfVector4>();

    /// <summary>
    /// HalfVector4P normalizes scaled input identically in scalar and bulk conversions.
    /// </summary>
    [Fact]
    public void HalfVector4P_ScaledInputIsNormalized() => AssertScaledInputIsNormalized<HalfVector4P>();

    /// <summary>
    /// RgbaVector normalizes scaled input identically in scalar and bulk conversions.
    /// </summary>
    [Fact]
    public void RgbaVector_ScaledInputIsNormalized() => AssertScaledInputIsNormalized<RgbaVector>();

    /// <summary>
    /// RgbaHalf normalizes scaled input identically in scalar and bulk conversions.
    /// </summary>
    [Fact]
    public void RgbaHalf_ScaledInputIsNormalized() => AssertScaledInputIsNormalized<RgbaHalf>();

    /// <summary>
    /// RgbaHalfP normalizes scaled input identically in scalar and bulk conversions.
    /// </summary>
    [Fact]
    public void RgbaHalfP_ScaledInputIsNormalized() => AssertScaledInputIsNormalized<RgbaHalfP>();

    /// <summary>
    /// Raw half storage preserves IEEE special values while its scaled representation remains finite.
    /// </summary>
    [Fact]
    public void HalfVector4_NativeSpecialValuesHaveNormalizedScaledOutput() => AssertNativeSpecialValuesHaveNormalizedScaledOutput();

    /// <summary>
    /// Associated half-vector conversion uses the stored alpha ratio before normalizing RGB.
    /// </summary>
    [Fact]
    public void HalfVector4P_AssociatedScaledInputIsNormalized() => AssertAssociatedScaledInputIsNormalized<HalfVector4P>();

    /// <summary>
    /// Associated half-RGBA conversion uses the stored alpha ratio before normalizing RGB.
    /// </summary>
    [Fact]
    public void RgbaHalfP_AssociatedScaledInputIsNormalized() => AssertAssociatedScaledInputIsNormalized<RgbaHalfP>();

    /// <summary>
    /// Checks saturation and NaN handling without deriving expectations from the invalid-input path.
    /// </summary>
    /// <typeparam name="TPixel">The destination pixel format.</typeparam>
    private static void AssertScaledInputIsNormalized<TPixel>()
        where TPixel : unmanaged, IPixel<TPixel>
    {
        Vector4[] inputs =
        [
            new(float.PositiveInfinity, float.NegativeInfinity, float.NaN, 1F),
            new(2F, -2F, .5F, 1F),
            new(.25F, .5F, .75F, .5F),
            new(.25F, .5F, .75F, float.NaN),
            new(.25F, .5F, .75F, float.PositiveInfinity)
        ];

        Vector4[] normalized =
        [
            new(1F, 0F, 0F, 1F),
            new(1F, 0F, .5F, 1F),
            new(.25F, .5F, .75F, .5F),
            new(.25F, .5F, .75F, 0F),
            new(.25F, .5F, .75F, 1F)
        ];

        // Seventeen pixels exercise wide registers and the narrower remainder paths.
        Vector4[] source = new Vector4[17];
        TPixel[] expected = new TPixel[source.Length];
        TPixel[] actual = new TPixel[source.Length];

        for (int i = 0; i < source.Length; i++)
        {
            int sample = i % inputs.Length;
            source[i] = inputs[sample];
            expected[i] = TPixel.FromUnassociatedScaledVector4(normalized[sample]);
            Assert.Equal(expected[i], TPixel.FromUnassociatedScaledVector4(source[i]));
        }

        // Associated formats otherwise interpret the vectors using their native alpha representation.
        PixelOperations<TPixel>.Instance.FromVector4Destructive(Configuration.Default, source, actual, PixelConversionModifiers.Scale | PixelConversionModifiers.UnPremultiply);
        Assert.Equal(expected, actual);
    }

    /// <summary>
    /// Checks associated input against finite control values with the same represented color.
    /// </summary>
    /// <typeparam name="TPixel">The associated destination pixel format.</typeparam>
    private static void AssertAssociatedScaledInputIsNormalized<TPixel>()
        where TPixel : unmanaged, IPixel<TPixel>
    {
        Vector4[] inputs =
        [
            new(float.PositiveInfinity, float.NegativeInfinity, float.NaN, 1F),
            new(1F, .5F, 1.5F, 2F),
            new(.125F, .25F, .375F, .5F),
            new(.25F, .5F, .75F, float.NaN),
            new(.25F, .5F, .75F, float.PositiveInfinity)
        ];

        Vector4[] normalized =
        [
            new(1F, 0F, 0F, 1F),
            new(.5F, .25F, .75F, 1F),
            new(.125F, .25F, .375F, .5F),
            Vector4.Zero,
            new(0F, 0F, 0F, 1F)
        ];

        Vector4[] source = new Vector4[17];
        TPixel[] expected = new TPixel[source.Length];
        TPixel[] actual = new TPixel[source.Length];

        for (int i = 0; i < source.Length; i++)
        {
            int sample = i % inputs.Length;
            source[i] = inputs[sample];
            expected[i] = TPixel.FromAssociatedScaledVector4(normalized[sample]);
            Assert.Equal(expected[i], TPixel.FromAssociatedScaledVector4(source[i]));
        }

        PixelOperations<TPixel>.Instance.FromVector4Destructive(Configuration.Default, source, actual, PixelConversionModifiers.Scale | PixelConversionModifiers.Premultiply);
        Assert.Equal(expected, actual);
    }

    /// <summary>
    /// Checks native storage and every scaled output lane independently of integer conversion semantics.
    /// </summary>
    private static void AssertNativeSpecialValuesHaveNormalizedScaledOutput()
    {
        Vector4 native = new(float.PositiveInfinity, float.NegativeInfinity, float.NaN, 65504F);
        HalfVector4 pixel = HalfVector4.FromVector4(native);
        Assert.True(float.IsPositiveInfinity(pixel.ToVector4().X));
        Assert.True(float.IsNegativeInfinity(pixel.ToVector4().Y));
        Assert.True(float.IsNaN(pixel.ToVector4().Z));

        Vector4 expected = new(1F, 0F, 0F, 1F);
        Assert.Equal(expected, pixel.ToScaledVector4());
        Assert.Equal(1F, new HalfSingle(float.PositiveInfinity).ToScaledVector4().X);
        Assert.Equal(0F, new HalfSingle(float.NaN).ToScaledVector4().X);
        Assert.Equal(new Vector4(1F, 0F, 0F, 1F), new HalfVector2(new Vector2(float.PositiveInfinity, float.NaN)).ToScaledVector4());

        HalfVector4[] source = new HalfVector4[17];
        Vector4[] nativeSource = new Vector4[source.Length];
        Array.Fill(nativeSource, native);
        PixelOperations<HalfVector4>.Instance.FromVector4Destructive(Configuration.Default, nativeSource, source, PixelConversionModifiers.None);
        Assert.All(source, value => Assert.Equal(pixel.PackedValue, value.PackedValue));

        Vector4[] actual = new Vector4[source.Length];
        PixelOperations<HalfVector4>.Instance.ToVector4(Configuration.Default, source, actual, PixelConversionModifiers.Scale);
        Assert.All(actual, value => Assert.Equal(expected, value));
    }
}
