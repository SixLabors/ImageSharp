// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Buffers;
using SixLabors.ImageSharp.Memory;
using SixLabors.ImageSharp.PixelFormats;

namespace SixLabors.ImageSharp.Formats.Tiff.PhotometricInterpretation
{
    /// <summary>
    /// The base class for planar color decoders.
    /// </summary>
    /// <typeparam name="TPixel">The pixel format.</typeparam>
    internal abstract class TiffBasePlanarColorDecoder<TPixel>
        where TPixel : unmanaged, IPixel<TPixel>
    {
        /// <summary>
        /// Decodes source raw pixel data using the current photometric interpretation.
        /// </summary>
        /// <param name="data">The buffers to read image data from.</param>
        /// <param name="pixels">The image buffer to write pixels to.</param>
        /// <param name="left">The x-coordinate of the left-hand side of the image block.</param>
        /// <param name="top">The y-coordinate of the  top of the image block.</param>
        /// <param name="width">The width of the image block.</param>
        /// <param name="height">The height of the image block.</param>
        public abstract void Decode(IMemoryOwner<byte>[] data, Buffer2D<TPixel> pixels, int left, int top, int width, int height);
    }
}
