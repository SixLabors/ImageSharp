// Copyright (c) Six Labors and contributors.
// Licensed under the Apache License, Version 2.0.

using SixLabors.ImageSharp.PixelFormats;
using SixLabors.Primitives;

namespace SixLabors.ImageSharp.Processing.Processors
{
    internal static class ImageProcessorExtensions
    {
        public static void Apply(this IImageProcessor processor, Image source, Rectangle sourceRectangle)
        {
            var visitor = new ApplyVisitor(processor, sourceRectangle);
            source.AcceptVisitor(visitor);
        }

        /// <summary>
        /// Apply an <see cref="IImageProcessor"/> to a frame.
        /// Only works from processors implemented by an <see cref="ImageProcessor{TPixel}"/> subclass.
        /// </summary>
        internal static void Apply<TPixel>(
            this IImageProcessor processor,
            ImageFrame<TPixel> frame,
            Rectangle sourceRectangle,
            Configuration configuration)
            where TPixel : struct, IPixel<TPixel>
        {
            var processorImpl = (ImageProcessor<TPixel>)processor.CreatePixelSpecificProcessor<TPixel>();
            processorImpl.Apply(frame, sourceRectangle, configuration);
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
                var processorImpl = this.processor.CreatePixelSpecificProcessor<TPixel>();
                processorImpl.Apply(image, this.sourceRectangle);
            }
        }
    }
}