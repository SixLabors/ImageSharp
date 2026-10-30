// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Numerics;
using SixLabors.ImageSharp.Formats;
using SixLabors.ImageSharp.Formats.Jxl;
using SixLabors.ImageSharp.Metadata;
using SixLabors.ImageSharp.PixelFormats;

namespace SixLabors.ImageSharp.Tests.Formats.Jxl;

[Trait("Format", "Jxl")]
public class JxlMetadataTests
{
    /// <summary>
    /// Verifies the legal unsigned and floating-point precision boundaries.
    /// </summary>
    /// <param name="bits">The total sample precision.</param>
    /// <param name="exponent">The exponent precision, or zero for unsigned integers.</param>
    [Theory]
    [InlineData(1, 0)]
    [InlineData(31, 0)]
    [InlineData(5, 2)]
    [InlineData(16, 5)]
    [InlineData(24, 7)]
    [InlineData(32, 8)]
    public void BitDepthAcceptsLegalLayouts(byte bits, byte exponent)
    {
        JxlBitDepth depth = new(bits, exponent);

        Assert.Equal(bits, depth.BitsPerSample);
        Assert.Equal(exponent, depth.ExponentBits);
    }

    /// <summary>
    /// Verifies that integer, exponent and mantissa bounds are enforced together.
    /// </summary>
    /// <param name="bits">The total sample precision.</param>
    /// <param name="exponent">The exponent precision.</param>
    [Theory]
    [InlineData(0, 0)]
    [InlineData(32, 0)]
    [InlineData(255, 0)]
    [InlineData(16, 1)]
    [InlineData(16, 9)]
    [InlineData(4, 2)]
    [InlineData(33, 8)]
    public void BitDepthRejectsInvalidLayouts(byte bits, byte exponent)
        => Assert.Throws<ArgumentOutOfRangeException>(() => new JxlBitDepth(bits, exponent));

    /// <summary>
    /// Verifies public metadata boundaries, including the uninitialized value of a struct.
    /// </summary>
    [Fact]
    public void MetadataRejectsUninitializedBitDepthAndInvalidColorType()
    {
        JxlMetadata metadata = new();

        Assert.Throws<ArgumentOutOfRangeException>(() => metadata.BitDepth = default);
        Assert.Throws<ArgumentOutOfRangeException>(() => metadata.ColorType = (JxlColorType)byte.MaxValue);
        Assert.Throws<ArgumentOutOfRangeException>(() => new JxlExtraChannelMetadata(
            JxlExtraChannelType.Alpha,
            default,
            PixelAlphaRepresentation.Unassociated));

        Assert.Equal(new JxlBitDepth(8, 0), metadata.BitDepth);
        Assert.Equal(JxlColorType.Rgb, metadata.ColorType);
    }

    /// <summary>
    /// Verifies reserved channel types and the alpha representation required by each channel type.
    /// </summary>
    [Fact]
    public void ExtraChannelRejectsReservedTypesAndInvalidAlphaRepresentation()
    {
        JxlBitDepth depth = new(8, 0);

        Assert.Throws<ArgumentException>(() => new JxlExtraChannelMetadata((JxlExtraChannelType)7, depth, PixelAlphaRepresentation.None));
        Assert.Throws<ArgumentException>(() => new JxlExtraChannelMetadata(JxlExtraChannelType.Unknown, depth, PixelAlphaRepresentation.None));
        Assert.Throws<ArgumentException>(() => new JxlExtraChannelMetadata(JxlExtraChannelType.Alpha, depth, PixelAlphaRepresentation.None));
        Assert.Throws<ArgumentException>(() => new JxlExtraChannelMetadata(JxlExtraChannelType.Black, depth, PixelAlphaRepresentation.Associated));
    }

    /// <summary>
    /// Verifies that type-specific metadata stays within the values its header fields can represent.
    /// </summary>
    [Fact]
    public void ExtraChannelParametersRetainValuesAndRejectUnencodableRanges()
    {
        JxlBitDepth depth = new(8, 0);
        Vector4 spot = new((float)Half.MaxValue, -(float)Half.MaxValue, 0.123456F, 0.5F);
        JxlExtraChannelMetadata channel = new(JxlExtraChannelType.SpotColor, depth, PixelAlphaRepresentation.None)
        {
            SpotColor = spot,
            DimensionShift = 3
        };

        Assert.Equal(spot, channel.SpotColor);
        Assert.Equal(3, channel.DimensionShift);
        Assert.Equal(1, new JxlExtraChannelMetadata(JxlExtraChannelType.ColorFilterArray, depth, PixelAlphaRepresentation.None).ColorFilterArrayChannel);
        Assert.Throws<ArgumentOutOfRangeException>(() => new JxlExtraChannelMetadata(JxlExtraChannelType.Depth, depth, PixelAlphaRepresentation.None)
        {
            DimensionShift = 4
        });

        Assert.Throws<ArgumentOutOfRangeException>(() => new JxlExtraChannelMetadata(JxlExtraChannelType.ColorFilterArray, depth, PixelAlphaRepresentation.None)
        {
            ColorFilterArrayChannel = 275
        });

        foreach (float invalid in new[] { float.NaN, float.PositiveInfinity, float.NegativeInfinity, 65505F })
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => new JxlExtraChannelMetadata(JxlExtraChannelType.SpotColor, depth, PixelAlphaRepresentation.None)
            {
                SpotColor = new Vector4(invalid, 0, 0, 0)
            });
        }
    }

    /// <summary>
    /// Verifies the encoded name limit for both ASCII and multibyte UTF-8 labels.
    /// </summary>
    [Fact]
    public void ExtraChannelNameUsesEncodedByteLimit()
    {
        JxlBitDepth depth = new(8, 0);
        string label = new string('\u00E9', 535) + "a";
        JxlExtraChannelMetadata channel = new(JxlExtraChannelType.Black, depth, PixelAlphaRepresentation.None) { Name = label };

        Assert.Equal(label, channel.Name);
        Assert.Equal(JxlExtraChannelType.Black, channel.Type);
        Assert.Throws<ArgumentOutOfRangeException>(() => new JxlExtraChannelMetadata(
            JxlExtraChannelType.Black,
            depth,
            PixelAlphaRepresentation.None) { Name = new string('a', 1072) });

        Assert.Throws<ArgumentOutOfRangeException>(() => new JxlExtraChannelMetadata(
            JxlExtraChannelType.Black,
            depth,
            PixelAlphaRepresentation.None) { Name = new string('\u00E9', 536) });
    }

    /// <summary>
    /// Verifies each encoded color model without treating XYB or profile-defined components as RGB.
    /// </summary>
    /// <param name="colorType">The encoded JPEG XL model.</param>
    /// <param name="bits">The color sample precision.</param>
    /// <param name="exponent">The color exponent precision.</param>
    /// <param name="expectedComponents">The base component count.</param>
    /// <param name="expectedColor">The shared pixel color model.</param>
    [Theory]
    [InlineData(JxlColorType.Rgb, 8, 0, 3, PixelColorType.RGB)]
    [InlineData(JxlColorType.Luminance, 12, 0, 1, PixelColorType.Luminance)]
    [InlineData(JxlColorType.Xyb, 16, 5, 3, PixelColorType.Other)]
    [InlineData(JxlColorType.Other, 32, 8, 3, PixelColorType.Other)]
    public void GetPixelTypeInfoPreservesColorModel(JxlColorType colorType, byte bits, byte exponent, int expectedComponents, PixelColorType expectedColor)
    {
        JxlMetadata metadata = new() { ColorType = colorType, BitDepth = new JxlBitDepth(bits, exponent) };

        PixelTypeInfo info = metadata.GetPixelTypeInfo();
        PixelComponentInfo components = info.ComponentInfo.Value;

        Assert.Equal(bits * expectedComponents, info.BitsPerPixel);
        Assert.Equal(expectedComponents, components.ComponentCount);
        Assert.Equal((int)bits, components.GetComponentPrecision(0));
        Assert.Equal(expectedColor, info.ColorType);
        Assert.Equal(exponent == 0 ? PixelColorRange.Sdr : PixelColorRange.Extended, info.ColorRange);
        Assert.Equal(PixelAlphaRepresentation.None, info.AlphaRepresentation);
    }

    /// <summary>
    /// Verifies independent K and alpha precision, without counting unrelated extra channels as color components.
    /// </summary>
    [Fact]
    public void GetPixelTypeInfoPreservesCmykAndAlphaPrecision()
    {
        JxlMetadata metadata = new() { BitDepth = new JxlBitDepth(16, 0) };
        metadata.ExtraChannels.Add(new JxlExtraChannelMetadata(JxlExtraChannelType.Depth, new JxlBitDepth(32, 8), PixelAlphaRepresentation.None));
        metadata.ExtraChannels.Add(new JxlExtraChannelMetadata(JxlExtraChannelType.Black, new JxlBitDepth(12, 0), PixelAlphaRepresentation.None));
        metadata.ExtraChannels.Add(new JxlExtraChannelMetadata(JxlExtraChannelType.Alpha, new JxlBitDepth(8, 0), PixelAlphaRepresentation.Associated));

        PixelTypeInfo info = metadata.GetPixelTypeInfo();
        PixelComponentInfo components = info.ComponentInfo.Value;

        Assert.Equal(68, info.BitsPerPixel);
        Assert.Equal(5, components.ComponentCount);
        Assert.Equal(16, components.GetComponentPrecision(0));
        Assert.Equal(12, components.GetComponentPrecision(3));
        Assert.Equal(8, components.GetComponentPrecision(4));
        Assert.Equal(PixelColorType.CMYK | PixelColorType.Alpha, info.ColorType);
        Assert.Equal(PixelColorRange.Sdr, info.ColorRange);
        Assert.Equal(PixelAlphaRepresentation.Associated, info.AlphaRepresentation);
    }

    /// <summary>
    /// Verifies precision and range conversion through the existing format-connecting API.
    /// </summary>
    /// <param name="extended">Whether the input permits extended color samples.</param>
    /// <param name="bits">The source precision.</param>
    /// <param name="expectedBits">The JPEG XL precision.</param>
    /// <param name="expectedExponent">The JPEG XL exponent precision.</param>
    [Theory]
    [InlineData(false, 16, 16, 0)]
    [InlineData(false, 32, 32, 8)]
    [InlineData(true, 16, 32, 8)]
    public void ConnectingMetadataPreservesHighPrecisionAndExtendedValues(bool extended, int bits, byte expectedBits, byte expectedExponent)
    {
        FormatConnectingMetadata source = new()
        {
            PixelTypeInfo = new PixelTypeInfo(bits * 4)
            {
                ComponentInfo = PixelComponentInfo.Create(4, bits * 4, bits, bits, bits, bits),
                ColorType = PixelColorType.RGB | PixelColorType.Alpha,
                AlphaRepresentation = PixelAlphaRepresentation.Associated,
                ColorRange = extended ? PixelColorRange.Extended : PixelColorRange.Sdr
            },
            RepeatCount = 7
        };

        JxlMetadata metadata = JxlMetadata.FromFormatConnectingMetadata(source);

        Assert.Equal(new JxlBitDepth(expectedBits, expectedExponent), metadata.BitDepth);
        JxlExtraChannelMetadata alpha = Assert.Single(metadata.ExtraChannels);
        Assert.Equal(metadata.BitDepth, alpha.BitDepth);
        Assert.Equal(PixelAlphaRepresentation.Associated, alpha.AlphaRepresentation);
        Assert.Equal(7U, metadata.RepeatCount);
    }

    /// <summary>
    /// Verifies that a finite JPEG XL play count cannot become infinite during conversion to another format.
    /// </summary>
    /// <param name="count">The JPEG XL play count.</param>
    /// <param name="expected">The play count supported by the connecting metadata.</param>
    [Theory]
    [InlineData(0U, 0)]
    [InlineData(7U, 7)]
    [InlineData(uint.MaxValue, ushort.MaxValue)]
    public void ConnectingMetadataBoundsRepeatCountWithoutWrapping(uint count, ushort expected)
    {
        JxlMetadata metadata = new() { RepeatCount = count };

        Assert.Equal(expected, metadata.ToFormatConnectingMetadata().RepeatCount);
        Assert.Equal(count, metadata.RepeatCount);
    }

    /// <summary>
    /// Verifies cloning through the image metadata API without sharing mutable channel lists.
    /// </summary>
    [Fact]
    public void ImageMetadataCloneRetainsDescriptionAndIndependentChannelList()
    {
        ImageMetadata imageMetadata = new();
        JxlMetadata source = imageMetadata.GetFormatMetadata(JxlFormat.Instance);
        source.BitDepth = new JxlBitDepth(32, 8);
        source.ColorType = JxlColorType.Xyb;
        source.RepeatCount = uint.MaxValue;
        source.ExtraChannels.Add(new JxlExtraChannelMetadata(JxlExtraChannelType.Alpha, new JxlBitDepth(16, 5), PixelAlphaRepresentation.Unassociated));

        JxlMetadata clone = imageMetadata.DeepClone().GetFormatMetadata(JxlFormat.Instance);

        Assert.NotSame(source, clone);
        Assert.Equal(new JxlBitDepth(32, 8), clone.BitDepth);
        Assert.Equal(JxlColorType.Xyb, clone.ColorType);
        Assert.Equal(uint.MaxValue, clone.RepeatCount);
        JxlExtraChannelMetadata alpha = Assert.Single(clone.ExtraChannels);
        Assert.Equal(new JxlBitDepth(16, 5), alpha.BitDepth);

        clone.ExtraChannels.Clear();

        Assert.Single(source.ExtraChannels);
    }

    /// <summary>
    /// Verifies exact JPEG XL timing, bounded shared durations and replacement behavior through the frame metadata API.
    /// </summary>
    /// <param name="numerator">The exact duration numerator.</param>
    /// <param name="denominator">The exact duration denominator.</param>
    /// <param name="ticks">The expected bounded duration in TimeSpan ticks.</param>
    [Theory]
    [InlineData(1L, 25L, 400000L)]
    [InlineData(5004998999L, 1000000000L, 50049989L)]
    [InlineData(long.MaxValue, long.MaxValue, 10000000L)]
    [InlineData(4398046510080L, 1L, long.MaxValue)]
    [InlineData(-1L, 1L, 0L)]
    [InlineData(0L, 0L, 0L)]
    [InlineData(1L, 0L, long.MaxValue)]
    [InlineData(-1L, 0L, 0L)]
    public void FrameMetadataRetainsDisplayDelayWithoutRepeatingNativeBlending(long numerator, long denominator, long ticks)
    {
        ImageFrameMetadata frameMetadata = new();
        JxlFrameMetadata source = frameMetadata.GetFormatMetadata(JxlFormat.Instance);
        source.FrameDelay = new LongRational(numerator, denominator);

        JxlFrameMetadata clone = frameMetadata.DeepClone().GetFormatMetadata(JxlFormat.Instance);
        FormatConnectingFrameMetadata connecting = clone.ToFormatConnectingFrameMetadata();
        JxlFrameMetadata restored = JxlFrameMetadata.FromFormatConnectingFrameMetadata(connecting);

        Assert.NotSame(source, clone);
        Assert.Equal(new LongRational(numerator, denominator), clone.FrameDelay);
        Assert.Equal(TimeSpan.FromTicks(ticks), connecting.Duration);
        Assert.Equal(FrameBlendMode.Source, connecting.BlendMode);
        Assert.Equal(FrameDisposalMode.DoNotDispose, connecting.DisposalMode);

        // Only the shared duration loses fractional ticks or values beyond TimeSpan's range.
        // Cloning and conversion must leave the original JPEG XL fraction unchanged.
        Assert.Equal(new LongRational(ticks, TimeSpan.TicksPerSecond).Simplify(), restored.FrameDelay);
        Assert.Equal(new LongRational(numerator, denominator), source.FrameDelay);
    }
}
