// <copyright file="ColorBlindness.cs" company="James Jackson-South">
// Copyright (c) James Jackson-South and contributors.
// Licensed under the Apache License, Version 2.0.
// </copyright>

namespace ImageProcessorCore
{
    using Processors;

    /// <summary>
    /// Extension methods for the <see cref="Image{TColor, TPacked}"/> type.
    /// </summary>
    public static partial class ImageExtensions
    {
        /// <summary>
        /// Applies the given colorblindness simulator to the image.
        /// </summary>
        /// <typeparam name="TColor">The pixel format.</typeparam>
        /// <typeparam name="TPacked">The packed format. <example>uint, long, float.</example></typeparam>
        /// <param name="source">The image this method extends.</param>
        /// <param name="colorBlindness">The type of color blindness simulator to apply.</param>
        /// <returns>The <see cref="Image{TColor, TPacked}"/>.</returns>
        public static Image<TColor, TPacked> ColorBlindness<TColor, TPacked>(this Image<TColor, TPacked> source, ColorBlindness colorBlindness)
            where TColor : struct, IPackedVector<TPacked>
            where TPacked : struct
        {
            return ColorBlindness(source, colorBlindness, source.Bounds);
        }

        /// <summary>
        /// Applies the given colorblindness simulator to the image.
        /// </summary>
        /// <typeparam name="TColor">The pixel format.</typeparam>
        /// <typeparam name="TPacked">The packed format. <example>uint, long, float.</example></typeparam>
        /// <param name="source">The image this method extends.</param>
        /// <param name="colorBlindness">The type of color blindness simulator to apply.</param>
        /// <param name="rectangle">
        /// The <see cref="Rectangle"/> structure that specifies the portion of the image object to alter.
        /// </param>
        /// <returns>The <see cref="Image{TColor, TPacked}"/>.</returns>
        public static Image<TColor, TPacked> ColorBlindness<TColor, TPacked>(this Image<TColor, TPacked> source, ColorBlindness colorBlindness, Rectangle rectangle)
            where TColor : struct, IPackedVector<TPacked>
            where TPacked : struct
        {
            IImageFilter<TColor, TPacked> processor;

            switch (colorBlindness)
            {
                case ImageProcessorCore.ColorBlindness.Achromatomaly:
                    processor = new AchromatomalyProcessor<TColor, TPacked>();
                    break;

                case ImageProcessorCore.ColorBlindness.Achromatopsia:
                    processor = new AchromatopsiaProcessor<TColor, TPacked>();
                    break;

                case ImageProcessorCore.ColorBlindness.Deuteranomaly:
                    processor = new DeuteranomalyProcessor<TColor, TPacked>();
                    break;

                case ImageProcessorCore.ColorBlindness.Deuteranopia:
                    processor = new DeuteranopiaProcessor<TColor, TPacked>();
                    break;

                case ImageProcessorCore.ColorBlindness.Protanomaly:
                    processor = new ProtanomalyProcessor<TColor, TPacked>();
                    break;

                case ImageProcessorCore.ColorBlindness.Protanopia:
                    processor = new ProtanopiaProcessor<TColor, TPacked>();
                    break;

                case ImageProcessorCore.ColorBlindness.Tritanomaly:
                    processor = new TritanomalyProcessor<TColor, TPacked>();
                    break;

                default:
                    processor = new TritanopiaProcessor<TColor, TPacked>();
                    break;
            }

            return source.Process(rectangle, processor);
        }
    }
}
