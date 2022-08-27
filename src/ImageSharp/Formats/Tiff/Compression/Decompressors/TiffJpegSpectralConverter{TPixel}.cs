// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using SixLabors.ImageSharp.Formats.Jpeg.Components;
using SixLabors.ImageSharp.Formats.Jpeg.Components.Decoder;
using SixLabors.ImageSharp.Formats.Tiff.Constants;
using SixLabors.ImageSharp.PixelFormats;

namespace SixLabors.ImageSharp.Formats.Tiff.Compression.Decompressors
{
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
            JpegColorSpace colorSpace = GetJpegColorSpaceFromPhotometricInterpretation(this.photometricInterpretation);
            return JpegColorConverterBase.GetConverter(colorSpace, frame.Precision);
        }

        /// <remarks>
        /// This converter must be used only for RGB and YCbCr color spaces for performance reasons.
        /// For grayscale images <see cref="GrayJpegSpectralConverter{TPixel}"/> must be used.
        /// </remarks>
        private static JpegColorSpace GetJpegColorSpaceFromPhotometricInterpretation(TiffPhotometricInterpretation interpretation)
            => interpretation switch
            {
                TiffPhotometricInterpretation.Rgb => JpegColorSpace.RGB,
                TiffPhotometricInterpretation.YCbCr => JpegColorSpace.RGB,
                _ => throw new InvalidImageContentException($"Invalid tiff photometric interpretation for jpeg encoding: {interpretation}"),
            };
    }
}
