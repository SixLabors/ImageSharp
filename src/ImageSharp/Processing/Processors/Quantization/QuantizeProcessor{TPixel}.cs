// Copyright (c) Six Labors.
// Licensed under the Apache License, Version 2.0.

using System;
using System.Runtime.CompilerServices;
using SixLabors.ImageSharp.Advanced;
using SixLabors.ImageSharp.Memory;
using SixLabors.ImageSharp.PixelFormats;

namespace SixLabors.ImageSharp.Processing.Processors.Quantization
{
    /// <summary>
    /// Enables the quantization of images to reduce the number of colors used in the image palette.
    /// </summary>
    /// <typeparam name="TPixel">The pixel format.</typeparam>
    internal class QuantizeProcessor<TPixel> : ImageProcessor<TPixel>
        where TPixel : unmanaged, IPixel<TPixel>
    {
        private readonly IQuantizer quantizer;

        /// <summary>
        /// Initializes a new instance of the <see cref="QuantizeProcessor{TPixel}"/> class.
        /// </summary>
        /// <param name="configuration">The configuration which allows altering default behaviour or extending the library.</param>
        /// <param name="quantizer">The quantizer used to reduce the color palette.</param>
        /// <param name="source">The source <see cref="Image{TPixel}"/> for the current processor instance.</param>
        /// <param name="sourceRectangle">The source area to process for the current processor instance.</param>
        public QuantizeProcessor(Configuration configuration, IQuantizer quantizer, Image<TPixel> source, Rectangle sourceRectangle)
            : base(configuration, source, sourceRectangle)
        {
            Guard.NotNull(quantizer, nameof(quantizer));
            this.quantizer = quantizer;
        }

        /// <inheritdoc />
        protected override void OnFrameApply(ImageFrame<TPixel> source)
        {
            var interest = Rectangle.Intersect(source.Bounds(), this.SourceRectangle);

            Configuration configuration = this.Configuration;
            using IQuantizer<TPixel> frameQuantizer = this.quantizer.CreatePixelSpecificQuantizer<TPixel>(configuration);
            using IndexedImageFrame<TPixel> quantized = frameQuantizer.BuildPaletteAndQuantizeFrame(source, interest);

            ReadOnlySpan<TPixel> paletteSpan = quantized.Palette.Span;
            int offsetY = interest.Top;
            int offsetX = interest.Left;

            for (int y = interest.Y; y < interest.Height; y++)
            {
                Span<TPixel> row = source.GetPixelRowSpan(y);
                ReadOnlySpan<byte> quantizedRow = quantized.GetPixelRowSpan(y - offsetY);

                for (int x = interest.Left; x < interest.Right; x++)
                {
                    row[x] = paletteSpan[quantizedRow[x - offsetX]];
                }
            }
        }
    }
}
