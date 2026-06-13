// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using SixLabors.ImageSharp.PixelFormats;

namespace SixLabors.ImageSharp.Formats.Heif;

/// <summary>
/// Factory for item decoders inside the HEIF container format.
/// </summary>
internal class HeifCompressionFactory
{
    /// <summary>
    /// Get a decoder implementation.
    /// </summary>
    public static IHeifItemDecoder<TPixel>? GetDecoder<TPixel>(Heif4CharCode type)
        where TPixel : unmanaged, IPixel<TPixel> => type switch
        {
            Heif4CharCode.Jpeg => new JpegHeifItemDecoder<TPixel>(),
            Heif4CharCode.Av01 => new Av1HeifItemDecoder<TPixel>(),
            _ => null
        };
}
