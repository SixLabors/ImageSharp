// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Runtime.CompilerServices;
using SixLabors.ImageSharp.PixelFormats;

namespace SixLabors.ImageSharp.Formats
{
    /// <summary>
    /// Contains information about the pixels that make up an images visual data.
    /// </summary>
    public class PixelTypeInfo
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="PixelTypeInfo"/> class.
        /// </summary>
        /// <param name="bitsPerPixel">Color depth, in number of bits per pixel.</param>
        public PixelTypeInfo(int bitsPerPixel)
        {
            this.BitsPerPixel = bitsPerPixel;
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="PixelTypeInfo"/> class.
        /// </summary>
        /// <param name="bitsPerPixel">Color depth, in number of bits per pixel.</param>
        /// <param name="alpha">The pixel alpha transparency behavior.</param>
        public PixelTypeInfo(int bitsPerPixel, PixelAlphaRepresentation alpha)
        {
            this.BitsPerPixel = bitsPerPixel;
            this.AlphaRepresentation = alpha;
        }

        /// <summary>
        /// Gets color depth, in number of bits per pixel.
        /// </summary>
        public int BitsPerPixel { get; }

        /// <summary>
        /// Gets the pixel alpha transparency behavior.
        /// <see langword="null"/> means unknown, unspecified.
        /// </summary>
        public PixelAlphaRepresentation? AlphaRepresentation { get; }

        internal static PixelTypeInfo Create<TPixel>()
            where TPixel : unmanaged, IPixel<TPixel> =>
            new PixelTypeInfo(Unsafe.SizeOf<TPixel>() * 8);

        internal static PixelTypeInfo Create<TPixel>(PixelAlphaRepresentation alpha)
            where TPixel : unmanaged, IPixel<TPixel>
        {
            return new PixelTypeInfo(Unsafe.SizeOf<TPixel>() * 8, alpha);
        }
    }
}
