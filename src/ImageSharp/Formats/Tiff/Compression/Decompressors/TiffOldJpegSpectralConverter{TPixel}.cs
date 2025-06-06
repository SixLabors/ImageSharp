// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using SixLabors.ImageSharp.Formats.Jpeg.Components;
using SixLabors.ImageSharp.Formats.Jpeg.Components.Decoder;
using SixLabors.ImageSharp.Formats.Tiff.Constants;
using SixLabors.ImageSharp.PixelFormats;

namespace SixLabors.ImageSharp.Formats.Tiff.Compression.Decompressors;

/// <summary>
/// Spectral converter for YCbCr TIFF's which use the OldJPEG compression.
/// The jpeg data should be always treated as YCbCr color space.
/// </summary>
/// <typeparam name="TPixel">The type of the pixel.</typeparam>
internal sealed class TiffOldJpegSpectralConverter<TPixel> : SpectralConverter<TPixel>
    where TPixel : unmanaged, IPixel<TPixel>
{
    private readonly TiffPhotometricInterpretation photometricInterpretation;

    /// <summary>
    /// Initializes a new instance of the <see cref="TiffOldJpegSpectralConverter{TPixel}"/> class.
    /// </summary>
    /// <param name="configuration">The configuration.</param>
    /// <param name="photometricInterpretation">Tiff photometric interpretation.</param>
    public TiffOldJpegSpectralConverter(Configuration configuration, TiffPhotometricInterpretation photometricInterpretation)
        : base(configuration)
        => this.photometricInterpretation = photometricInterpretation;

    /// <inheritdoc/>
    protected override JpegColorConverterBase GetColorConverter(JpegFrame frame, IRawJpegData jpegData)
    {
        JpegColorSpace colorSpace = GetJpegColorSpaceFromPhotometricInterpretation(this.photometricInterpretation, jpegData);
        return JpegColorConverterBase.GetConverter(colorSpace, frame.Precision);
    }

    private static JpegColorSpace GetJpegColorSpaceFromPhotometricInterpretation(TiffPhotometricInterpretation interpretation, IRawJpegData data)
        => interpretation switch
        {
            // Like libtiff: Always treat the pixel data as YCbCr when the data is compressed with old jpeg compression.
            TiffPhotometricInterpretation.Rgb => JpegColorSpace.YCbCr,
            TiffPhotometricInterpretation.Separated => data.ColorSpace == JpegColorSpace.Ycck ? JpegColorSpace.TiffYccK : JpegColorSpace.TiffCmyk,
            TiffPhotometricInterpretation.YCbCr => JpegColorSpace.YCbCr,
            _ => throw new InvalidImageContentException($"Invalid tiff photometric interpretation for jpeg encoding: {interpretation}"),
        };
}
