// // Copyright (c) Six Labors and contributors.
// // Licensed under the Apache License, Version 2.0.

using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Primitives;

namespace SixLabors.ImageSharp.Processing
{
    internal static class DrawingHelpers
    {
        public static DenseMatrix<TPixel> ToPixelMatrix<TPixel>(this DenseMatrix<Color> colorMatrix, Configuration configuration)
            where TPixel : struct, IPixel<TPixel>
        {
            DenseMatrix<TPixel> result = new DenseMatrix<TPixel>(colorMatrix.Columns, colorMatrix.Rows);
            Color.ToPixel(configuration, colorMatrix.Span, result.Span);
            return result;
        }
    }
}