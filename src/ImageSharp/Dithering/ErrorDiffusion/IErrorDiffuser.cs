// Copyright (c) Six Labors and contributors.
// Licensed under the Apache License, Version 2.0.

using SixLabors.ImageSharp.PixelFormats;

namespace SixLabors.ImageSharp.Dithering
{
    /// <summary>
    /// Encapsulates properties and methods required to perfom diffused error dithering on an image.
    /// </summary>
    public interface IErrorDiffuser
    {
        /// <summary>
        /// Transforms the image applying the dither matrix. This method alters the input pixels array
        /// </summary>
        /// <param name="image">The image</param>
        /// <param name="source">The source pixel</param>
        /// <param name="transformed">The transformed pixel</param>
        /// <param name="x">The column index.</param>
        /// <param name="y">The row index.</param>
        /// <param name="minX">The minimum column value.</param>
        /// <param name="minY">The minimum row value.</param>
        /// <param name="maxX">The maximum column value.</param>
        /// <param name="maxY">The maximum row value.</param>
        /// <typeparam name="TPixel">The pixel format.</typeparam>
        void Dither<TPixel>(ImageFrame<TPixel> image, TPixel source, TPixel transformed, int x, int y, int minX, int minY, int maxX, int maxY)
            where TPixel : struct, IPixel<TPixel>;

        /// <summary>
        /// Transforms the image applying the dither matrix. This method alters the input pixels array
        /// </summary>
        /// <param name="image">The image</param>
        /// <param name="source">The source pixel</param>
        /// <param name="transformed">The transformed pixel</param>
        /// <param name="x">The column index.</param>
        /// <param name="y">The row index.</param>
        /// <param name="minX">The minimum column value.</param>
        /// <param name="minY">The minimum row value.</param>
        /// <param name="maxX">The maximum column value.</param>
        /// <param name="maxY">The maximum row value.</param>
        /// <param name="replacePixel">
        /// Whether to replace the pixel at the given coordinates with the transformed value.
        /// Generally this would be true for standard two-color dithering but when used in conjunction with color quantization this should be false.
        /// </param>
        /// <typeparam name="TPixel">The pixel format.</typeparam>
        void Dither<TPixel>(ImageFrame<TPixel> image, TPixel source, TPixel transformed, int x, int y, int minX, int minY, int maxX, int maxY, bool replacePixel)
            where TPixel : struct, IPixel<TPixel>;
    }
}
