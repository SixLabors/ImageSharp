// Copyright (c) Six Labors and contributors.
// Licensed under the Apache License, Version 2.0.

using SixLabors.ImageSharp.PixelFormats;
using SixLabors.Primitives;

namespace SixLabors.ImageSharp.Processing.Processors
{
    public interface IImageProcessor
    {
        IImageProcessor<TPixel> CreatePixelSpecificProcessor<TPixel>()
            where TPixel : struct, IPixel<TPixel>;
    }

    /// <summary>
    /// Encapsulates methods to alter the pixels of an image.
    /// </summary>
    /// <typeparam name="TPixel">The pixel format.</typeparam>
    public interface IImageProcessor<TPixel>
        where TPixel : struct, IPixel<TPixel>
    {
        /// <summary>
        /// Applies the process to the specified portion of the specified <see cref="ImageFrame{TPixel}"/>.
        /// </summary>
        /// <param name="source">The source image. Cannot be null.</param>
        /// <param name="sourceRectangle">
        /// The <see cref="Rectangle"/> structure that specifies the portion of the image object to draw.
        /// </param>
        /// <exception cref="System.ArgumentNullException">
        /// <paramref name="source"/> is null.
        /// </exception>
        /// <exception cref="System.ArgumentException">
        /// <paramref name="sourceRectangle"/> doesn't fit the dimension of the image.
        /// </exception>
        void Apply(Image<TPixel> source, Rectangle sourceRectangle);
    }

    internal static class ImageProcessorExtensions
    {
        public static void Apply(this IImageProcessor processor, Image source, Rectangle sourceRectangle)
        {
            var visitor = new ApplyVisitor(processor, sourceRectangle);
            source.AcceptVisitor(visitor);
        }

        private class ApplyVisitor : IImageVisitor
        {
            private readonly IImageProcessor processor;

            private readonly Rectangle sourceRectangle;

            public ApplyVisitor(IImageProcessor processor, Rectangle sourceRectangle)
            {
                this.processor = processor;
                this.sourceRectangle = sourceRectangle;
            }

            public void Visit<TPixel>(Image<TPixel> image)
                where TPixel : struct, IPixel<TPixel>
            {
                var processorImpl = processor.CreatePixelSpecificProcessor<TPixel>();
                processorImpl.Apply(image, this.sourceRectangle);
            }
        }
    }
}