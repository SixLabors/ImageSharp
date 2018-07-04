// Copyright (c) Six Labors and contributors.
// Licensed under the Apache License, Version 2.0.

using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing.Processors;
using SixLabors.ImageSharp.Processing.Processors.Filters;
using SixLabors.Primitives;

namespace SixLabors.ImageSharp.Processing
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
        public static IImageProcessingContext<TPixel> ColorBlindness<TPixel>(this IImageProcessingContext<TPixel> source, ColorBlindnessMode colorBlindness)
            where TPixel : struct, IPixel<TPixel>
            => source.ApplyProcessor(GetProcessor<TPixel>(colorBlindness));

        /// <summary>
        /// Applies the given colorblindness simulator to the image.
        /// </summary>
        /// <typeparam name="TPixel">The pixel format.</typeparam>
        /// <param name="source">The image this method extends.</param>
        /// <param name="colorBlindnessMode">The type of color blindness simulator to apply.</param>
        /// <param name="rectangle">
        /// The <see cref="Rectangle"/> structure that specifies the portion of the image object to alter.
        /// </param>
        /// <returns>The <see cref="Image{TPixel}"/>.</returns>
        public static IImageProcessingContext<TPixel> ColorBlindness<TPixel>(this IImageProcessingContext<TPixel> source, ColorBlindnessMode colorBlindnessMode, Rectangle rectangle)
            where TPixel : struct, IPixel<TPixel>
            => source.ApplyProcessor(GetProcessor<TPixel>(colorBlindnessMode), rectangle);

        private static IImageProcessor<TPixel> GetProcessor<TPixel>(ColorBlindnessMode colorBlindness)
            where TPixel : struct, IPixel<TPixel>
        {
            switch (colorBlindness)
            {
                case ColorBlindnessMode.Achromatomaly:
                    return new AchromatomalyProcessor<TPixel>();
                case ColorBlindnessMode.Achromatopsia:
                    return new AchromatopsiaProcessor<TPixel>();
                case ColorBlindnessMode.Deuteranomaly:
                    return new DeuteranomalyProcessor<TPixel>();
                case ColorBlindnessMode.Deuteranopia:
                    return new DeuteranopiaProcessor<TPixel>();
                case ColorBlindnessMode.Protanomaly:
                    return new ProtanomalyProcessor<TPixel>();
                case ColorBlindnessMode.Protanopia:
                    return new ProtanopiaProcessor<TPixel>();
                case ColorBlindnessMode.Tritanomaly:
                    return new TritanomalyProcessor<TPixel>();
                default:
                    return new TritanopiaProcessor<TPixel>();
            }
        }
    }
}