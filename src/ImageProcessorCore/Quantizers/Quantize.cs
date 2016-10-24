// <copyright file="Quantize.cs" company="James Jackson-South">
// Copyright (c) James Jackson-South and contributors.
// Licensed under the Apache License, Version 2.0.
// </copyright>

namespace ImageProcessorCore
{
    using ImageProcessorCore.Quantizers;

    /// <summary>
    /// Extension methods for the <see cref="Image{TColor, TPacked}"/> type.
    /// </summary>
    public static partial class ImageExtensions
    {
        /// <summary>
        /// Applies quantization to the image.
        /// </summary>
        /// <typeparam name="TColor">The pixel format.</typeparam>
        /// <typeparam name="TPacked">The packed format. <example>uint, long, float.</example></typeparam>
        /// <param name="source">The image this method extends.</param>
        /// <param name="mode">The quantization mode to apply to perform the operation.</param>
        /// <param name="maxColors">The maximum number of colors to return. Defaults to 256.</param>
        /// <returns>The <see cref="Image{TColor, TPacked}"/>.</returns>
        public static Image<TColor, TPacked> Quantize<TColor, TPacked>(this Image<TColor, TPacked> source, Quantization mode = Quantization.Octree, int maxColors = 256)
            where TColor : struct, IPackedVector<TPacked>
            where TPacked : struct
        {
            IQuantizer<TColor, TPacked> quantizer;
            switch (mode)
            {
                case Quantization.Wu:
                    quantizer = new WuQuantizer<TColor, TPacked>();
                    break;

                case Quantization.Palette:
                    quantizer = new PaletteQuantizer<TColor, TPacked>();
                    break;

                default:
                    quantizer = new OctreeQuantizer<TColor, TPacked>();
                    break;
            }

            return Quantize(source, quantizer, maxColors);
        }

        /// <summary>
        /// Applies quantization to the image.
        /// </summary>
        /// <typeparam name="TColor">The pixel format.</typeparam>
        /// <typeparam name="TPacked">The packed format. <example>uint, long, float.</example></typeparam>
        /// <param name="source">The image this method extends.</param>
        /// <param name="quantizer">The quantizer to apply to perform the operation.</param>
        /// <param name="maxColors">The maximum number of colors to return.</param>
        /// <returns>The <see cref="Image{TColor, TPacked}"/>.</returns>
        public static Image<TColor, TPacked> Quantize<TColor, TPacked>(this Image<TColor, TPacked> source, IQuantizer<TColor, TPacked> quantizer, int maxColors)
            where TColor : struct, IPackedVector<TPacked>
            where TPacked : struct
        {
            QuantizedImage<TColor, TPacked> quantizedImage = quantizer.Quantize(source, maxColors);
            source.SetPixels(source.Width, source.Height, quantizedImage.ToImage().Pixels);
            return source;
        }
    }
}
