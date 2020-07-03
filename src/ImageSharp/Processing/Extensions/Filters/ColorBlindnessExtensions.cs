// Copyright (c) Six Labors.
// Licensed under the Apache License, Version 2.0.

using SixLabors.ImageSharp.Processing.Processors;
using SixLabors.ImageSharp.Processing.Processors.Filters;

namespace SixLabors.ImageSharp.Processing
{
    /// <summary>
    /// Defines extensions that simulate the effects of various color blindness disorders on an <see cref="Image"/>
    /// using Mutate/Clone.
    /// </summary>
    public static class ColorBlindnessExtensions
    {
        /// <summary>
        /// Applies the given colorblindness simulator to the image.
        /// </summary>
        /// <param name="source">The image this method extends.</param>
        /// <param name="colorBlindness">The type of color blindness simulator to apply.</param>
        /// <returns>The <see cref="IImageProcessingContext"/> to allow chaining of operations.</returns>
        public static IImageProcessingContext ColorBlindness(this IImageProcessingContext source, ColorBlindnessMode colorBlindness)
            => source.ApplyProcessor(GetProcessor(colorBlindness));

        /// <summary>
        /// Applies the given colorblindness simulator to the image.
        /// </summary>
        /// <param name="source">The image this method extends.</param>
        /// <param name="colorBlindnessMode">The type of color blindness simulator to apply.</param>
        /// <param name="rectangle">
        /// The <see cref="Rectangle"/> structure that specifies the portion of the image object to alter.
        /// </param>
        /// <returns>The <see cref="IImageProcessingContext"/> to allow chaining of operations.</returns>
        public static IImageProcessingContext ColorBlindness(this IImageProcessingContext source, ColorBlindnessMode colorBlindnessMode, Rectangle rectangle)
            => source.ApplyProcessor(GetProcessor(colorBlindnessMode), rectangle);

        private static IImageProcessor GetProcessor(ColorBlindnessMode colorBlindness)
        {
            switch (colorBlindness)
            {
                case ColorBlindnessMode.Achromatomaly:
                    return new AchromatomalyProcessor();
                case ColorBlindnessMode.Achromatopsia:
                    return new AchromatopsiaProcessor();
                case ColorBlindnessMode.Deuteranomaly:
                    return new DeuteranomalyProcessor();
                case ColorBlindnessMode.Deuteranopia:
                    return new DeuteranopiaProcessor();
                case ColorBlindnessMode.Protanomaly:
                    return new ProtanomalyProcessor();
                case ColorBlindnessMode.Protanopia:
                    return new ProtanopiaProcessor();
                case ColorBlindnessMode.Tritanomaly:
                    return new TritanomalyProcessor();
                default:
                    return new TritanopiaProcessor();
            }
        }
    }
}