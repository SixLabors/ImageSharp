// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Numerics.Tensors;
using SixLabors.ImageSharp.Formats.Jpeg.Components;
using SixLabors.ImageSharp.Formats.Jpeg.Components.Decoder;
using SixLabors.ImageSharp.Formats.Tiff.Constants;
using SixLabors.ImageSharp.Metadata.Profiles.Icc;
using SixLabors.ImageSharp.PixelFormats;

namespace SixLabors.ImageSharp.Formats.Tiff.Compression.Decompressors;

/// <summary>
/// Spectral converter for gray TIFF's which use the JPEG compression.
/// </summary>
/// <typeparam name="TPixel">The type of the pixel.</typeparam>
internal sealed class GrayJpegSpectralConverter<TPixel> : SpectralConverter<TPixel>
    where TPixel : unmanaged, IPixel<TPixel>
{
    private readonly bool whiteIsZero;

    /// <summary>
    /// Initializes a new instance of the <see cref="GrayJpegSpectralConverter{TPixel}"/> class.
    /// </summary>
    /// <param name="configuration">The configuration.</param>
    /// <param name="photometricInterpretation">The TIFF grayscale sample interpretation.</param>
    public GrayJpegSpectralConverter(Configuration configuration, TiffPhotometricInterpretation photometricInterpretation)
        : base(configuration)
    {
        this.whiteIsZero = photometricInterpretation == TiffPhotometricInterpretation.WhiteIsZero;
    }

    /// <inheritdoc/>
    protected override void PrepareColorComponents(in JpegColorConverterBase.ComponentValues values, int precision, IccProfile? iccProfile)
    {
        if (this.whiteIsZero)
        {
            float maximumValue = (1 << precision) - 1;

            // WhiteIsZero changes the luminance interpretation before JPEG grayscale and ICC conversion.
            TensorPrimitives.Subtract(maximumValue, values.Component0, values.Component0);
        }
    }

    /// <inheritdoc/>
    protected override JpegColorConverterBase GetColorConverter(JpegFrame frame, IRawJpegData jpegData) => JpegColorConverterBase.GetConverter(JpegColorSpace.Grayscale, frame.Precision);
}
