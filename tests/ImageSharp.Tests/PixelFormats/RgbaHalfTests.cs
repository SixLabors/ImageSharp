// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Numerics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Tests.TestUtilities;

namespace SixLabors.ImageSharp.Tests.PixelFormats;

/// <summary>
/// Tests the unit-range binary16 RGBA pixel formats.
/// </summary>
[Trait("Category", "PixelFormats")]
public class RgbaHalfTests
{
    /// <summary>
    /// Verifies that the unassociated format has the native layout required by RGBA binary16 surfaces.
    /// </summary>
    [Fact]
    public void RgbaHalfHasRgbaBinary16Layout()
    {
        RgbaHalf pixel = new(.25F, .5F, .75F, 1F);
        ulong expected = BitConverter.HalfToUInt16Bits((Half).25F)
            | ((ulong)BitConverter.HalfToUInt16Bits((Half).5F) << 16)
            | ((ulong)BitConverter.HalfToUInt16Bits((Half).75F) << 32)
            | ((ulong)BitConverter.HalfToUInt16Bits((Half)1F) << 48);

        Assert.Equal(8, Unsafe.SizeOf<RgbaHalf>());
        Assert.Equal(expected, pixel.PackedValue);
        Assert.Equal(new Vector4(.25F, .5F, .75F, 1F), pixel.ToVector4());
        Assert.Equal(pixel.ToVector4(), pixel.ToScaledVector4());
    }

    /// <summary>
    /// Verifies that zero-filled binary16 storage represents transparent black without affine remapping.
    /// </summary>
    [Fact]
    public void RgbaHalfDefaultIsTransparentBlack()
    {
        RgbaHalf pixel = default;

        Assert.Equal(Vector4.Zero, pixel.ToVector4());
        Assert.Equal(Vector4.Zero, pixel.ToScaledVector4());
    }

    /// <summary>
    /// Verifies that scaled input is clamped to the pixel format's unit color range.
    /// </summary>
    [Fact]
    public void RgbaHalfFromScaledVector4ClampsToUnitRange()
    {
        RgbaHalf pixel = RgbaHalf.FromScaledVector4(new Vector4(-1F, .5F, 2F, 1F));

        Assert.Equal(new Vector4(0F, .5F, 1F, 1F), pixel.ToScaledVector4());
    }

    /// <summary>
    /// Verifies that the associated format stores associated binary16 components in the same RGBA order.
    /// </summary>
    [Fact]
    public void RgbaHalfPHasAssociatedRgbaBinary16Layout()
    {
        RgbaHalfP pixel = new(.125F, .25F, .375F, .5F);
        ulong expected = BitConverter.HalfToUInt16Bits((Half).125F)
            | ((ulong)BitConverter.HalfToUInt16Bits((Half).25F) << 16)
            | ((ulong)BitConverter.HalfToUInt16Bits((Half).375F) << 32)
            | ((ulong)BitConverter.HalfToUInt16Bits((Half).5F) << 48);

        Assert.Equal(8, Unsafe.SizeOf<RgbaHalfP>());
        Assert.Equal(expected, pixel.PackedValue);
        Assert.Equal(new Vector4(.125F, .25F, .375F, .5F), pixel.ToAssociatedScaledVector4());
    }

    /// <summary>
    /// Verifies that zero-filled associated binary16 storage represents transparent black.
    /// </summary>
    [Fact]
    public void RgbaHalfPDefaultIsTransparentBlack()
    {
        RgbaHalfP pixel = default;

        Assert.Equal(Vector4.Zero, pixel.ToVector4());
        Assert.Equal(Vector4.Zero, pixel.ToScaledVector4());
        Assert.Equal(Vector4.Zero, pixel.ToUnassociatedScaledVector4());
    }

    /// <summary>
    /// Verifies that association uses the alpha value that survives binary16 quantization.
    /// </summary>
    [Fact]
    public void RgbaHalfPQuantizesAlphaBeforeAssociation()
    {
        Vector4 source = new(.75F, .5F, .25F, 1F / 3F);
        float storedAlpha = (float)(Half)source.W;
        RgbaHalfP pixel = RgbaHalfP.FromUnassociatedScaledVector4(source);

        Assert.Equal((Half)(source.X * storedAlpha), pixel.R);
        Assert.Equal((Half)(source.Y * storedAlpha), pixel.G);
        Assert.Equal((Half)(source.Z * storedAlpha), pixel.B);
        Assert.Equal((Half)storedAlpha, pixel.A);
    }

    /// <summary>
    /// Verifies every bulk representation path against its scalar pixel contract at each SIMD width and tail boundary.
    /// </summary>
    [Fact]
    public void RgbaHalfBulkConversionsMatchScalarAcrossHardwareWidths()
        => FeatureTestRunner.RunWithHwIntrinsicsFeature(
            static () => AssertBulkConversionsMatchScalar<RgbaHalf>(),
            HwIntrinsics.AllowAll | HwIntrinsics.DisableAVX512F | HwIntrinsics.DisableAVX | HwIntrinsics.DisableHWIntrinsic);

    /// <summary>
    /// Verifies every associated bulk representation path against its scalar pixel contract at each SIMD width and tail boundary.
    /// </summary>
    [Fact]
    public void RgbaHalfPBulkConversionsMatchScalarAcrossHardwareWidths()
        => FeatureTestRunner.RunWithHwIntrinsicsFeature(
            static () => AssertBulkConversionsMatchScalar<RgbaHalfP>(),
            HwIntrinsics.AllowAll | HwIntrinsics.DisableAVX512F | HwIntrinsics.DisableAVX | HwIntrinsics.DisableHWIntrinsic);

    /// <summary>
    /// Verifies the declared component precision and alpha representation for the unassociated format.
    /// </summary>
    [Fact]
    public void RgbaHalfPixelInformationIsCorrect() => AssertPixelInformation<RgbaHalf>(PixelAlphaRepresentation.Unassociated);

    /// <summary>
    /// Verifies the declared component precision and alpha representation for the associated format.
    /// </summary>
    [Fact]
    public void RgbaHalfPPixelInformationIsCorrect() => AssertPixelInformation<RgbaHalfP>(PixelAlphaRepresentation.Associated);

    /// <summary>
    /// Compares bulk conversions with the corresponding scalar pixel operations for representative vector widths and remainders.
    /// </summary>
    /// <typeparam name="TPixel">The binary16 pixel format to test.</typeparam>
    private static void AssertBulkConversionsMatchScalar<TPixel>()
        where TPixel : unmanaged, IPixel<TPixel>
    {
        int[] lengths = [0, 1, 2, 3, 4, 5, 7, 8, 9, 15, 16, 17, 259];
        PixelConversionModifiers[] modifiers =
        [
            PixelConversionModifiers.None,
            PixelConversionModifiers.Scale,
            PixelConversionModifiers.Premultiply,
            PixelConversionModifiers.Scale | PixelConversionModifiers.Premultiply,
            PixelConversionModifiers.UnPremultiply,
            PixelConversionModifiers.Scale | PixelConversionModifiers.UnPremultiply
        ];

        foreach (int length in lengths)
        {
            TPixel[] pixels = new TPixel[length];

            for (int i = 0; i < length; i++)
            {
                pixels[i] = TPixel.FromUnassociatedScaledVector4(CreateUnassociatedVector(i));
            }

            foreach (PixelConversionModifiers modifier in modifiers)
            {
                Vector4[] expectedVectors = new Vector4[length];
                Vector4[] actualVectors = new Vector4[length];
                Vector4[] sourceVectors = new Vector4[length];
                TPixel[] expectedPixels = new TPixel[length];
                TPixel[] actualPixels = new TPixel[length];
                bool associated = RequestsAssociated<TPixel>(modifier);
                bool scaled = modifier.IsDefined(PixelConversionModifiers.Scale);

                for (int i = 0; i < length; i++)
                {
                    expectedVectors[i] = ToScalarVector(pixels[i], associated, scaled);
                    sourceVectors[i] = CreateUnassociatedVector(i + 19);

                    if (associated)
                    {
                        Numerics.Premultiply(ref sourceVectors[i]);
                    }

                    expectedPixels[i] = FromScalarVector<TPixel>(sourceVectors[i], associated, scaled);
                }

                PixelOperations<TPixel>.Instance.ToVector4(Configuration.Default, pixels, actualVectors, modifier);
                PixelOperations<TPixel>.Instance.FromVector4Destructive(Configuration.Default, sourceVectors.AsSpan(), actualPixels, modifier);

                Assert.Equal(expectedVectors, actualVectors);
                Assert.Equal(expectedPixels, actualPixels);
            }
        }
    }

    /// <summary>
    /// Creates a deterministic unassociated unit-range vector for bulk conversion tests.
    /// </summary>
    /// <param name="index">The source index used to vary the component values.</param>
    /// <returns>The generated vector.</returns>
    private static Vector4 CreateUnassociatedVector(int index)
        => new(
            ((index * 37) % 101) / 100F,
            ((index * 53) % 101) / 100F,
            ((index * 71) % 101) / 100F,
            ((index * 89) % 101) / 100F);

    /// <summary>
    /// Resolves whether a modifier combination requests associated output under the pixel format's native representation.
    /// </summary>
    /// <typeparam name="TPixel">The pixel format whose native representation participates in modifier resolution.</typeparam>
    /// <param name="modifiers">The conversion modifiers.</param>
    /// <returns><see langword="true"/> when the resulting vector representation is associated.</returns>
    private static bool RequestsAssociated<TPixel>(PixelConversionModifiers modifiers)
        where TPixel : unmanaged, IPixel<TPixel>
        => modifiers.IsDefined(PixelConversionModifiers.Premultiply)
            || (TPixel.GetPixelTypeInfo().AlphaRepresentation == PixelAlphaRepresentation.Associated
                && !modifiers.IsDefined(PixelConversionModifiers.UnPremultiply));

    /// <summary>
    /// Converts one pixel through the scalar API selected by the requested representation and range.
    /// </summary>
    /// <typeparam name="TPixel">The source pixel format.</typeparam>
    /// <param name="pixel">The source pixel.</param>
    /// <param name="associated">Whether the result should use associated alpha.</param>
    /// <param name="scaled">Whether the result should use the scaled range.</param>
    /// <returns>The converted vector.</returns>
    private static Vector4 ToScalarVector<TPixel>(TPixel pixel, bool associated, bool scaled)
        where TPixel : unmanaged, IPixel<TPixel>
        => (associated, scaled) switch
        {
            (true, true) => pixel.ToAssociatedScaledVector4(),
            (true, false) => pixel.ToAssociatedVector4(),
            (false, true) => pixel.ToUnassociatedScaledVector4(),
            _ => pixel.ToUnassociatedVector4()
        };

    /// <summary>
    /// Converts one vector through the scalar API selected by its representation and range.
    /// </summary>
    /// <typeparam name="TPixel">The destination pixel format.</typeparam>
    /// <param name="vector">The source vector.</param>
    /// <param name="associated">Whether the source uses associated alpha.</param>
    /// <param name="scaled">Whether the source uses the scaled range.</param>
    /// <returns>The converted pixel.</returns>
    private static TPixel FromScalarVector<TPixel>(Vector4 vector, bool associated, bool scaled)
        where TPixel : unmanaged, IPixel<TPixel>
        => (associated, scaled) switch
        {
            (true, true) => TPixel.FromAssociatedScaledVector4(vector),
            (true, false) => TPixel.FromAssociatedVector4(vector),
            (false, true) => TPixel.FromUnassociatedScaledVector4(vector),
            _ => TPixel.FromUnassociatedVector4(vector)
        };

    /// <summary>
    /// Verifies the component layout and alpha metadata exposed by a binary16 pixel format.
    /// </summary>
    /// <typeparam name="TPixel">The pixel format to inspect.</typeparam>
    /// <param name="alphaRepresentation">The expected alpha representation.</param>
    private static void AssertPixelInformation<TPixel>(PixelAlphaRepresentation alphaRepresentation)
        where TPixel : unmanaged, IPixel<TPixel>
    {
        PixelTypeInfo info = TPixel.GetPixelTypeInfo();
        PixelComponentInfo componentInfo = info.ComponentInfo.Value;

        Assert.Equal(64, info.BitsPerPixel);
        Assert.Equal(alphaRepresentation, info.AlphaRepresentation);
        Assert.Equal(PixelColorType.RGB | PixelColorType.Alpha, info.ColorType);
        Assert.Equal(4, componentInfo.ComponentCount);
        Assert.Equal(0, componentInfo.Padding);

        for (int i = 0; i < componentInfo.ComponentCount; i++)
        {
            Assert.Equal(16, componentInfo.GetComponentPrecision(i));
        }
    }
}

/// <summary>
/// Applies the shared associated-alpha contract to <see cref="RgbaHalfP"/>.
/// </summary>
public class RgbaHalfPAssociatedAlphaTests : AssociatedAlphaPixelTests<RgbaHalfP>
{
}
