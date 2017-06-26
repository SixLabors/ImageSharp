// <copyright file="ColorBlindness.cs" company="James Jackson-South">
// Copyright (c) James Jackson-South and contributors.
// Licensed under the Apache License, Version 2.0.
// </copyright>

namespace ImageSharp
{
    using System;

    using ImageSharp.PixelFormats;

    using ImageSharp.Processing;
    using Processing.Processors;
    using SixLabors.Primitives;

    /// <summary>
    /// Extension methods for the <see cref="Image{TPixel}"/> type.
    /// </summary>
    public static partial class ImageExtensions
    {
        /// <summary>
        /// Applies the given colorblindness simulator to the image.
        /// </summary>
        /// <typeparam name="TPixel">The pixel format.</typeparam>
        /// <param name="source">The image this method extends.</param>
        /// <param name="colorBlindness">The type of color blindness simulator to apply.</param>
        /// <returns>The <see cref="Image{TPixel}"/>.</returns>
        public static Image<TPixel> ColorBlindness<TPixel>(this Image<TPixel> source, ColorBlindness colorBlindness)
            where TPixel : struct, IPixel<TPixel>
        {
            return ColorBlindness(source, colorBlindness, source.Bounds);
        }

        /// <summary>
        /// Applies the given colorblindness simulator to the image.
        /// </summary>
        /// <typeparam name="TPixel">The pixel format.</typeparam>
        /// <param name="source">The image this method extends.</param>
        /// <param name="colorBlindness">The type of color blindness simulator to apply.</param>
        /// <param name="rectangle">
        /// The <see cref="Rectangle"/> structure that specifies the portion of the image object to alter.
        /// </param>
        /// <returns>The <see cref="Image{TPixel}"/>.</returns>
        public static Image<TPixel> ColorBlindness<TPixel>(this Image<TPixel> source, ColorBlindness colorBlindness, Rectangle rectangle)
            where TPixel : struct, IPixel<TPixel>
        {
            IImageProcessor<TPixel> processor;

            switch (colorBlindness)
            {
                case ImageSharp.Processing.ColorBlindness.Achromatomaly:
                    processor = new AchromatomalyProcessor<TPixel>();
                    break;

                case ImageSharp.Processing.ColorBlindness.Achromatopsia:
                    processor = new AchromatopsiaProcessor<TPixel>();
                    break;

                case ImageSharp.Processing.ColorBlindness.Deuteranomaly:
                    processor = new DeuteranomalyProcessor<TPixel>();
                    break;

                case ImageSharp.Processing.ColorBlindness.Deuteranopia:
                    processor = new DeuteranopiaProcessor<TPixel>();
                    break;

                case ImageSharp.Processing.ColorBlindness.Protanomaly:
                    processor = new ProtanomalyProcessor<TPixel>();
                    break;

                case ImageSharp.Processing.ColorBlindness.Protanopia:
                    processor = new ProtanopiaProcessor<TPixel>();
                    break;

                case ImageSharp.Processing.ColorBlindness.Tritanomaly:
                    processor = new TritanomalyProcessor<TPixel>();
                    break;

                default:
                    processor = new TritanopiaProcessor<TPixel>();
                    break;
            }

            source.ApplyProcessor(processor, rectangle);
            return source;
        }
    }
}
