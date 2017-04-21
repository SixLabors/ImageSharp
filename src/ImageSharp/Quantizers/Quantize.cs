// <copyright file="Quantize.cs" company="James Jackson-South">
// Copyright (c) James Jackson-South and contributors.
// Licensed under the Apache License, Version 2.0.
// </copyright>

namespace ImageSharp
{
    using System;
    using System.Threading.Tasks;

    using ImageSharp.PixelFormats;
    using ImageSharp.Quantizers;

    /// <summary>
    /// Extension methods for the <see cref="Image{TPixel}"/> type.
    /// </summary>
    public static partial class ImageExtensions
    {
        /// <summary>
        /// Applies quantization to the image.
        /// </summary>
        /// <typeparam name="TPixel">The pixel format.</typeparam>
        /// <param name="source">The image this method extends.</param>
        /// <param name="mode">The quantization mode to apply to perform the operation.</param>
        /// <param name="maxColors">The maximum number of colors to return. Defaults to 256.</param>
        /// <returns>The <see cref="Image{TPixel}"/>.</returns>
        public static Image<TPixel> Quantize<TPixel>(this Image<TPixel> source, Quantization mode = Quantization.Octree, int maxColors = 256)
            where TPixel : struct, IPixel<TPixel>
        {
            IQuantizer<TPixel> quantizer;
            switch (mode)
            {
                case Quantization.Wu:
                    quantizer = new WuQuantizer<TPixel>();
                    break;

                case Quantization.Palette:
                    quantizer = new PaletteQuantizer<TPixel>();
                    break;

                default:
                    quantizer = new OctreeQuantizer<TPixel>();
                    break;
            }

            return Quantize(source, quantizer, maxColors);
        }

        /// <summary>
        /// Applies quantization to the image.
        /// </summary>
        /// <typeparam name="TPixel">The pixel format.</typeparam>
        /// <param name="source">The image this method extends.</param>
        /// <param name="quantizer">The quantizer to apply to perform the operation.</param>
        /// <param name="maxColors">The maximum number of colors to return.</param>
        /// <returns>The <see cref="Image{TPixel}"/>.</returns>
        public static Image<TPixel> Quantize<TPixel>(this Image<TPixel> source, IQuantizer<TPixel> quantizer, int maxColors)
            where TPixel : struct, IPixel<TPixel>
        {
            QuantizedImage<TPixel> quantized = quantizer.Quantize(source, maxColors);
            int palleteCount = quantized.Palette.Length - 1;

            using (PixelAccessor<TPixel> pixels = new PixelAccessor<TPixel>(quantized.Width, quantized.Height))
            {
                Parallel.For(
                    0,
                    pixels.Height,
                    source.Configuration.ParallelOptions,
                    y =>
                    {
                        for (int x = 0; x < pixels.Width; x++)
                        {
                            int i = x + (y * pixels.Width);
                            TPixel color = quantized.Palette[Math.Min(palleteCount, quantized.Pixels[i])];
                            pixels[x, y] = color;
                        }
                    });

                source.SwapPixelsBuffers(pixels);
                return source;
            }
        }
    }
}