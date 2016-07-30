// <copyright file="Quantize.cs" company="James Jackson-South">
// Copyright (c) James Jackson-South and contributors.
// Licensed under the Apache License, Version 2.0.
// </copyright>

using ImageProcessorCore.Quantizers;

namespace ImageProcessorCore
{
    /// <summary>
    /// Extension methods for the <see cref="Image{T,TP}"/> type.
    /// </summary>
    public static partial class ImageExtensions
    {
        /// <summary>
        /// Applies quantization to the image.
        /// </summary>
        /// <typeparam name="T">The pixel format.</typeparam>
        /// <typeparam name="TP">The packed format. <example>long, float.</example></typeparam>
        /// <param name="source">The image this method extends.</param>
        /// <param name="mode">The quantization mode to apply to perform the operation.</param>
        /// <param name="maxColors">The maximum number of colors to return. Defaults to 256.</param>
        /// <returns>The <see cref="Image{T,TP}"/>.</returns>
        public static Image<T, TP> Quantize<T, TP>(this Image<T, TP> source, Quantization mode = Quantization.Octree, int maxColors = 256)
            where T : IPackedVector<TP>
            where TP : struct
        {
            IQuantizer<T, TP> quantizer;
            switch (mode)
            {
                case Quantization.Wu:
                    quantizer = new WuQuantizer<T, TP>();
                    break;

                case Quantization.Palette:
                    quantizer = new PaletteQuantizer<T, TP>();
                    break;

                default:
                    quantizer = new OctreeQuantizer<T, TP>();
                    break;
            }

            return Quantize(source, quantizer, maxColors);
        }

        /// <summary>
        /// Applies quantization to the image.
        /// </summary>
        /// <typeparam name="T">The pixel format.</typeparam>
        /// <typeparam name="TP">The packed format. <example>long, float.</example></typeparam>
        /// <param name="source">The image this method extends.</param>
        /// <param name="quantizer">The quantizer to apply to perform the operation.</param>
        /// <param name="maxColors">The maximum number of colors to return.</param>
        /// <returns>The <see cref="Image{T,TP}"/>.</returns>
        public static Image<T, TP> Quantize<T, TP>(this Image<T, TP> source, IQuantizer<T, TP> quantizer, int maxColors)
            where T : IPackedVector<TP>
            where TP : struct
        {
            QuantizedImage<T, TP> quantizedImage = quantizer.Quantize(source, maxColors);
            source.SetPixels(source.Width, source.Height, quantizedImage.ToImage().Pixels);
            return source;
        }
    }
}
