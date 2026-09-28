// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using SixLabors.ImageSharp.PixelFormats;

namespace SixLabors.ImageSharp.Formats.Tiff.PhotometricInterpretation;

/// <summary>
/// Implements the 'BlackIsZero' photometric interpretation for 32-bit float grayscale images.
/// </summary>
/// <typeparam name="TPixel">The type of pixel format.</typeparam>
internal class BlackIsZero32FloatTiffColor<TPixel> : TiffFloatGrayscaleColor<TPixel>
    where TPixel : unmanaged, IPixel<TPixel>
{
    /// <summary>
    /// Initializes a new instance of the <see cref="BlackIsZero32FloatTiffColor{TPixel}" /> class.
    /// </summary>
    /// <param name="configuration">The configuration used by bulk pixel conversion.</param>
    /// <param name="isBigEndian">if set to <c>true</c> decodes the pixel data as big endian, otherwise as little endian.</param>
    public BlackIsZero32FloatTiffColor(Configuration configuration, bool isBigEndian)
        : base(configuration, isBigEndian, whiteIsZero: false)
    {
    }
}
