// Copyright (c) Six Labors and contributors.
// Licensed under the Apache License, Version 2.0.

using System;
using System.Numerics;
using System.Threading.Tasks;
using SixLabors.ImageSharp.Advanced;
using SixLabors.ImageSharp.ParallelUtils;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.Primitives;

namespace SixLabors.ImageSharp.Processing.Processors.Filters
{
    /// <summary>
    /// Provides methods that accept a <see cref="Matrix4x4"/> matrix to apply free-form filters to images.
    /// </summary>
    /// <typeparam name="TPixel">The pixel format.</typeparam>
    internal class FilterProcessor<TPixel> : ImageProcessor<TPixel>
        where TPixel : struct, IPixel<TPixel>
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="FilterProcessor{TPixel}"/> class.
        /// </summary>
        /// <param name="matrix">The matrix used to apply the image filter</param>
        public FilterProcessor(Matrix4x4 matrix)
        {
            this.Matrix = matrix;
        }

        /// <summary>
        /// Gets the <see cref="Matrix4x4"/> used to apply the image filter.
        /// </summary>
        public Matrix4x4 Matrix { get; }

        /// <inheritdoc/>
        protected override void OnFrameApply(ImageFrame<TPixel> source, Rectangle sourceRectangle, Configuration configuration)
        {
            var interest = Rectangle.Intersect(sourceRectangle, source.Bounds());

            Matrix4x4 matrix = this.Matrix;

            ParallelHelper.IterateRows(
                interest,
                configuration,
                rows =>
                    {
                        for (int y = rows.Min; y < rows.Max; y++)
                        {
                            Span<TPixel> row = source.GetPixelRowSpan(y);

                            for (int x = interest.X; x < interest.Right; x++)
                            {
                                ref TPixel pixel = ref row[x];
                                var vector = Vector4.Transform(pixel.ToVector4(), matrix);
                                pixel.PackFromVector4(vector);
                            }
                        }
                    });
        }
    }
}