// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using SixLabors.ImageSharp.Formats.Jpeg.Components;
using SixLabors.ImageSharp.Formats.Jpeg.Components.Decoder;
using SixLabors.ImageSharp.Formats.Tiff.Constants;
using SixLabors.ImageSharp.PixelFormats;

namespace SixLabors.ImageSharp.Formats.Tiff.Compression.Decompressors;

/// <summary>
/// Spectral converter for TIFF JPEG strips with a TIFF-declared component layout.
/// </summary>
/// <typeparam name="TPixel">The type of the pixel.</typeparam>
internal sealed class TiffJpegSpectralConverter<TPixel> : SpectralConverter<TPixel>
    where TPixel : unmanaged, IPixel<TPixel>
{
    private readonly TiffPhotometricInterpretation photometricInterpretation;

    /// <summary>
    /// Initializes a new instance of the <see cref="TiffJpegSpectralConverter{TPixel}"/> class.
    /// </summary>
    /// <param name="configuration">The configuration.</param>
    /// <param name="photometricInterpretation">Tiff photometric interpretation.</param>
    public TiffJpegSpectralConverter(Configuration configuration, TiffPhotometricInterpretation photometricInterpretation)
        : base(configuration)
        => this.photometricInterpretation = photometricInterpretation;

    /// <inheritdoc/>
    protected override JpegColorConverterBase GetColorConverter(JpegFrame frame, IRawJpegData jpegData)
    {
        JpegColorSpace colorSpace = GetJpegColorSpace(this.photometricInterpretation, jpegData);
        return JpegColorConverterBase.GetConverter(colorSpace, frame.Precision);
    }

    /// <summary>
    /// RGB strips retain identity JPEG conversion. YCbCr, separated CMYK, and YccK use
    /// the JPEG converters for their encoded component models and source ICC profiles.
    /// For grayscale images <see cref="GrayJpegSpectralConverter{TPixel}"/> must be used.
    /// </summary>
    /// <param name="interpretation">
    /// The <see cref="TiffPhotometricInterpretation"/> to convert to a <see cref="JpegColorSpace"/>.
    /// </param>
    /// <param name="data">
    /// The <see cref="IRawJpegData"/> containing the color space information.
    /// </param>
    /// <exception cref="InvalidImageContentException">
    /// Thrown when the <paramref name="interpretation"/> is not supported for JPEG decoding.
    /// </exception>
    private static JpegColorSpace GetJpegColorSpace(TiffPhotometricInterpretation interpretation, IRawJpegData data) => interpretation switch
    {
        TiffPhotometricInterpretation.Rgb => JpegColorSpace.RGB,
        TiffPhotometricInterpretation.Separated => data.ColorSpace == JpegColorSpace.Ycck ? JpegColorSpace.TiffYccK : JpegColorSpace.TiffCmyk,
        TiffPhotometricInterpretation.YCbCr => JpegColorSpace.YCbCr,
        _ => throw new InvalidImageContentException($"Invalid TIFF photometric interpretation for JPEG decoding: {interpretation}"),
    };
}
