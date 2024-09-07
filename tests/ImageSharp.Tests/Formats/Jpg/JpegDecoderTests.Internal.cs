// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using SixLabors.ImageSharp.Formats.Jpeg;
using SixLabors.ImageSharp.Formats.Jpeg.Components;
using SixLabors.ImageSharp.Formats.Jpeg.Components.Decoder;

// ReSharper disable InconsistentNaming
namespace SixLabors.ImageSharp.Tests.Formats.Jpg;

[Trait("Format", "Jpg")]
public partial class JpegDecoderTests
{
    [Theory]
    [InlineData(1, 0, JpegColorSpace.Grayscale)]
    [InlineData(3, JpegConstants.Adobe.ColorTransformUnknown, JpegColorSpace.RGB)]
    [InlineData(3, JpegConstants.Adobe.ColorTransformYCbCr, JpegColorSpace.YCbCr)]
    [InlineData(4, JpegConstants.Adobe.ColorTransformUnknown, JpegColorSpace.Cmyk)]
    [InlineData(4, JpegConstants.Adobe.ColorTransformYcck, JpegColorSpace.Ycck)]
    internal void DeduceJpegColorSpaceAdobeMarker_ShouldReturnValidColorSpace(byte componentCount, byte adobeFlag, JpegColorSpace expectedColorSpace)
    {
        byte[] adobeMarkerPayload = [0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, adobeFlag];
        ProfileResolver.AdobeMarker.CopyTo(adobeMarkerPayload);
        _ = AdobeMarker.TryParse(adobeMarkerPayload, out AdobeMarker adobeMarker);

        JpegColorSpace actualColorSpace = JpegDecoderCore.DeduceJpegColorSpace(componentCount, ref adobeMarker);

        Assert.Equal(expectedColorSpace, actualColorSpace);
    }

    [Theory]
    [InlineData(2)]
    [InlineData(5)]
    public void DeduceJpegColorSpaceAdobeMarker_ShouldThrowOnUnsupportedComponentCount(byte componentCount)
    {
        AdobeMarker adobeMarker = default;

        Assert.Throws<NotSupportedException>(() => JpegDecoderCore.DeduceJpegColorSpace(componentCount, ref adobeMarker));
    }

    [Theory]
    [InlineData(1, JpegColorSpace.Grayscale)]
    [InlineData(3, JpegColorSpace.YCbCr)]
    [InlineData(4, JpegColorSpace.Cmyk)]
    internal void DeduceJpegColorSpace_ShouldReturnValidColorSpace(byte componentCount, JpegColorSpace expectedColorSpace)
    {
        JpegColorSpace actualColorSpace = JpegDecoderCore.DeduceJpegColorSpace(componentCount);

        Assert.Equal(expectedColorSpace, actualColorSpace);
    }

    [Theory]
    [InlineData(2)]
    [InlineData(5)]
    public void DeduceJpegColorSpace_ShouldThrowOnUnsupportedComponentCount(byte componentCount)
        => Assert.Throws<NotSupportedException>(() => JpegDecoderCore.DeduceJpegColorSpace(componentCount));
}
