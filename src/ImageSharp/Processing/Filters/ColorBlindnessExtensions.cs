// Copyright (c) Six Labors and contributors.
// Licensed under the Apache License, Version 2.0.

using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing.Filters.Processors;
using SixLabors.ImageSharp.Processing.Processors;
using SixLabors.Primitives;

namespace SixLabors.ImageSharp.Processing.Filters
{
    /// <summary>
    /// Adds extensions that simulate the effects of various color blindness disorders to the <see cref="Image{TPixel}"/> type.
    /// </summary>
    public static class ColorBlindnessExtensions
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
            => source.ApplyProcessor(GetProcessor<TPixel>(colorBlindness));

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
            => source.ApplyProcessor(GetProcessor<TPixel>(colorBlindness), rectangle);

        private static IImageProcessor<TPixel> GetProcessor<TPixel>(ColorBlindness colorBlindness)
            where TPixel : struct, IPixel<TPixel>
        {
            switch (colorBlindness)
            {
                case Filters.ColorBlindness.Achromatomaly:
                    return new AchromatomalyProcessor<TPixel>();
                case Filters.ColorBlindness.Achromatopsia:
                    return new AchromatopsiaProcessor<TPixel>();
                case Filters.ColorBlindness.Deuteranomaly:
                    return new DeuteranomalyProcessor<TPixel>();
                case Filters.ColorBlindness.Deuteranopia:
                    return new DeuteranopiaProcessor<TPixel>();
                case Filters.ColorBlindness.Protanomaly:
                    return new ProtanomalyProcessor<TPixel>();
                case Filters.ColorBlindness.Protanopia:
                    return new ProtanopiaProcessor<TPixel>();
                case Filters.ColorBlindness.Tritanomaly:
                    return new TritanomalyProcessor<TPixel>();
                default:
                    return new TritanopiaProcessor<TPixel>();
            }
        }
    }
}