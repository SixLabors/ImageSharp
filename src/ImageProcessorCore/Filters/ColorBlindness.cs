// <copyright file="ColorBlindness.cs" company="James Jackson-South">
// Copyright (c) James Jackson-South and contributors.
// Licensed under the Apache License, Version 2.0.
// </copyright>-------------------------------------------------------------------------------------------------------------------

namespace ImageProcessorCore
{
    using Processors;

    /// <summary>
    /// Extension methods for the <see cref="Image"/> type.
    /// </summary>
    public static partial class ImageExtensions
    {
        /// <summary>
        /// Applies the given colorblindness simulator to the image.
        /// </summary>
        /// <typeparam name="T">The pixel format.</typeparam>
        /// <typeparam name="TP">The packed format. <example>long, float.</example></typeparam>
        /// <param name="source">The image this method extends.</param>
        /// <param name="colorBlindness">The type of color blindness simulator to apply.</param>
        /// <param name="progressHandler">A delegate which is called as progress is made processing the image.</param>
        /// <returns>The <see cref="Image{T,TP}"/>.</returns>
        public static Image<T, TP> ColorBlindness<T, TP>(this Image<T, TP> source, ColorBlindness colorBlindness, ProgressEventHandler progressHandler = null)
            where T : IPackedVector<TP>
            where TP : struct
        {
            return ColorBlindness(source, colorBlindness, source.Bounds, progressHandler);
        }

        /// <summary>
        /// Applies the given colorblindness simulator to the image.
        /// </summary>
        /// <typeparam name="T">The pixel format.</typeparam>
        /// <typeparam name="TP">The packed format. <example>long, float.</example></typeparam>
        /// <param name="source">The image this method extends.</param>
        /// <param name="colorBlindness">The type of color blindness simulator to apply.</param>
        /// <param name="rectangle">
        /// The <see cref="Rectangle"/> structure that specifies the portion of the image object to alter.
        /// </param>
        /// <param name="progressHandler">A delegate which is called as progress is made processing the image.</param>
        /// <returns>The <see cref="Image{T,TP}"/>.</returns>
        public static Image<T, TP> ColorBlindness<T, TP>(this Image<T, TP> source, ColorBlindness colorBlindness, Rectangle rectangle, ProgressEventHandler progressHandler = null)
            where T : IPackedVector<TP>
            where TP : struct
        {
            IImageProcessor<T, TP> processor;

            switch (colorBlindness)
            {
                case ImageProcessorCore.ColorBlindness.Achromatomaly:
                    processor = new AchromatomalyProcessor<T, TP>();
                    break;

                case ImageProcessorCore.ColorBlindness.Achromatopsia:
                    processor = new AchromatopsiaProcessor<T, TP>();
                    break;

                case ImageProcessorCore.ColorBlindness.Deuteranomaly:
                    processor = new DeuteranomalyProcessor<T, TP>();
                    break;

                case ImageProcessorCore.ColorBlindness.Deuteranopia:
                    processor = new DeuteranopiaProcessor<T, TP>();
                    break;

                case ImageProcessorCore.ColorBlindness.Protanomaly:
                    processor = new ProtanomalyProcessor<T, TP>();
                    break;

                case ImageProcessorCore.ColorBlindness.Protanopia:
                    processor = new ProtanopiaProcessor<T, TP>();
                    break;

                case ImageProcessorCore.ColorBlindness.Tritanomaly:
                    processor = new TritanomalyProcessor<T, TP>();
                    break;

                default:
                    processor = new TritanopiaProcessor<T, TP>();
                    break;
            }

            processor.OnProgress += progressHandler;

            try
            {
                return source.Process(rectangle, processor);
            }
            finally
            {
                processor.OnProgress -= progressHandler;
            }
        }
    }
}
