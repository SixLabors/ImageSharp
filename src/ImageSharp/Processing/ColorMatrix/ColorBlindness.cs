// Copyright (c) Six Labors and contributors.
// Licensed under the Apache License, Version 2.0.

using System;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;
using SixLabors.ImageSharp.Processing.Processors;
using SixLabors.Primitives;

namespace SixLabors.ImageSharp
{
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
        public static IImageProcessingContext<TPixel> ColorBlindness<TPixel>(this IImageProcessingContext<TPixel> source, ColorBlindness colorBlindness)
            where TPixel : struct, IPixel<TPixel>
        {
            source.ApplyProcessor(GetProcessor<TPixel>(colorBlindness));
            return source;
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
        public static IImageProcessingContext<TPixel> ColorBlindness<TPixel>(this IImageProcessingContext<TPixel> source, ColorBlindness colorBlindness, Rectangle rectangle)
            where TPixel : struct, IPixel<TPixel>
        {
            source.ApplyProcessor(GetProcessor<TPixel>(colorBlindness), rectangle);
            return source;
        }

        private static IImageProcessor<TPixel> GetProcessor<TPixel>(ColorBlindness colorBlindness)
            where TPixel : struct, IPixel<TPixel>
        {
            switch (colorBlindness)
            {
                case ImageSharp.Processing.ColorBlindness.Achromatomaly:
                    return new AchromatomalyProcessor<TPixel>();
                case ImageSharp.Processing.ColorBlindness.Achromatopsia:
                    return new AchromatopsiaProcessor<TPixel>();
                case ImageSharp.Processing.ColorBlindness.Deuteranomaly:
                    return new DeuteranomalyProcessor<TPixel>();
                case ImageSharp.Processing.ColorBlindness.Deuteranopia:
                    return new DeuteranopiaProcessor<TPixel>();
                case ImageSharp.Processing.ColorBlindness.Protanomaly:
                    return new ProtanomalyProcessor<TPixel>();
                case ImageSharp.Processing.ColorBlindness.Protanopia:
                    return new ProtanopiaProcessor<TPixel>();
                case ImageSharp.Processing.ColorBlindness.Tritanomaly:
                    return new TritanomalyProcessor<TPixel>();
                default:
                    return new TritanopiaProcessor<TPixel>();
            }
        }
    }
}
