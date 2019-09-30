// Copyright (c) Six Labors and contributors.
// Licensed under the Apache License, Version 2.0.

using SixLabors.ImageSharp.Advanced;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.Primitives;

namespace SixLabors.ImageSharp.Processing.Processors
{
    internal static class ImageProcessorExtensions
    {
        /// <summary>
        /// Executes the processor against the given source image and rectangle bounds.
        /// </summary>
        /// <param name="processor">The processor.</param>
        /// <param name="source">The source image.</param>
        /// <param name="sourceRectangle">The source bounds.</param>
        public static void Execute(this IImageProcessor processor, Image source, Rectangle sourceRectangle)
            => source.AcceptVisitor(new ExecuteVisitor(processor, sourceRectangle));

        private class ExecuteVisitor : IImageVisitor
        {
            private readonly IImageProcessor processor;
            private readonly Rectangle sourceRectangle;

            public ExecuteVisitor(IImageProcessor processor, Rectangle sourceRectangle)
            {
                this.processor = processor;
                this.sourceRectangle = sourceRectangle;
            }

            public void Visit<TPixel>(Image<TPixel> image)
                where TPixel : struct, IPixel<TPixel>
            {
                using (IImageProcessor<TPixel> processorImpl = this.processor.CreatePixelSpecificProcessor(image, this.sourceRectangle))
                {
                    processorImpl.Execute();
                }
            }
        }
    }
}
