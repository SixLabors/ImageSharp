// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using SixLabors.ImageSharp.Formats.Jpeg.Components;
using SixLabors.ImageSharp.Formats.Jpeg.Components.Decoder;
using SixLabors.ImageSharp.Formats.Tiff.Constants;
using SixLabors.ImageSharp.PixelFormats;

namespace SixLabors.ImageSharp.Formats.Tiff.Compression.Decompressors;

/// <summary>
/// Spectral converter for YCbCr TIFF's which use the JPEG compression.
/// The jpeg data should be always treated as RGB color space.
/// </summary>
/// <typeparam name="TPixel">The type of the pixel.</typeparam>
internal sealed class TiffJpegSpectralConverter<TPixel> : SpectralConverter<TPixel>
    where TPixel : unmanaged, IPixel<TPixel>
{
    private readonly TiffPhotometricInterpretation photometricInterpretation;

    /// <summary>
    /// Initializes a new instance of the <see cref="TiffJpegSpectralConverter{TPixel}"/> class.
    /// This Spectral converter will always convert the pixel data to RGB color.
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
    /// Photometric interpretation Rgb and YCbCr will be mapped to RGB colorspace, which means the jpeg decompression will leave the data as is (no color conversion).
    /// The color conversion will be done after the decompression. For Separated/CMYK/YCCK, the jpeg color converter will handle the color conversion,
    /// since the jpeg color converter needs to return RGB data and cannot return 4 component data.
    /// For grayscale images <see cref="GrayJpegSpectralConverter{TPixel}"/> must be used.
    /// </summary>
    /// <param name="interpretation">
    /// The <see cref="TiffPhotometricInterpretation"/> to convert to a <see cref="JpegColorSpace"/>.
    /// </param>
    /// <param name="data">
    /// The <see cref="IRawJpegData"/> containing the color space information.
    /// </param>
    /// <exception cref="InvalidImageContentException">
    /// Thrown when the <paramref name="interpretation"/> is not supported for JPEG encoding.
    /// </exception>
    private static JpegColorSpace GetJpegColorSpace(TiffPhotometricInterpretation interpretation, IRawJpegData data) => interpretation switch
    {
        TiffPhotometricInterpretation.Rgb => JpegColorSpace.RGB,
        TiffPhotometricInterpretation.Separated => data.ColorSpace == JpegColorSpace.Ycck ? JpegColorSpace.TiffYccK : JpegColorSpace.TiffCmyk,
        TiffPhotometricInterpretation.YCbCr => JpegColorSpace.RGB, // TODO: Why doesn't this use the YCbCr color space?
        _ => throw new InvalidImageContentException($"Invalid TIFF photometric interpretation for JPEG encoding: {interpretation}"),
    };
}
