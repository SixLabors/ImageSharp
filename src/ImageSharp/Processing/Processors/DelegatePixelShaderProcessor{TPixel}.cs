// Copyright (c) Six Labors and contributors.
// Licensed under the Apache License, Version 2.0.

using System;
using System.Numerics;

using SixLabors.ImageSharp.Advanced;
using SixLabors.ImageSharp.Advanced.ParallelUtils;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing.Delegates;
using SixLabors.Primitives;

namespace SixLabors.ImageSharp.Processing.Processors
{
    /// <summary>
    /// Performs simple binary threshold filtering against an image.
    /// </summary>
    /// <typeparam name="TPixel">The pixel format.</typeparam>
    internal class DelegatePixelShaderProcessor<TPixel> : ImageProcessor<TPixel>
        where TPixel : struct, IPixel<TPixel>
    {
        /// <summary>
        /// The user defined pixel shader.
        /// </summary>
        private readonly PixelShader pixelShader;

        /// <summary>
        /// Initializes a new instance of the <see cref="DelegatePixelShaderProcessor{TPixel}"/> class.
        /// </summary>
        /// <param name="definition">The <see cref="DelegatePixelShaderProcessor"/> defining the processor parameters.</param>
        /// <param name="source">The source <see cref="Image{TPixel}"/> for the current processor instance.</param>
        /// <param name="sourceRectangle">The source area to process for the current processor instance.</param>
        public DelegatePixelShaderProcessor(DelegatePixelShaderProcessor definition, Image<TPixel> source, Rectangle sourceRectangle)
            : base(source, sourceRectangle)
        {
            this.pixelShader = definition.PixelShader;
        }

        /// <inheritdoc/>
        protected override void OnFrameApply(ImageFrame<TPixel> source)
        {
            var interest = Rectangle.Intersect(this.SourceRectangle, source.Bounds());
            int startX = interest.X;
            PixelShader pixelShader = this.pixelShader;

            ParallelHelper.IterateRowsWithTempBuffer<Vector4>(
                interest,
                this.Configuration,
                (rows, vectorBuffer) =>
                {
                    for (int y = rows.Min; y < rows.Max; y++)
                    {
                        Span<Vector4> vectorSpan = vectorBuffer.Span;
                        int length = vectorSpan.Length;
                        Span<TPixel> rowSpan = source.GetPixelRowSpan(y).Slice(startX, length);
                        PixelOperations<TPixel>.Instance.ToVector4(this.Configuration, rowSpan, vectorSpan);

                        // Run the user defined pixel shader on the current row of pixels
                        pixelShader(vectorSpan);

                        PixelOperations<TPixel>.Instance.FromVector4Destructive(this.Configuration, vectorSpan, rowSpan);
                    }
                });
        }
    }
}
