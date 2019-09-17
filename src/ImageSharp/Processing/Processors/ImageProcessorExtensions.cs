// Copyright (c) Six Labors and contributors.
// Licensed under the Apache License, Version 2.0.

using SixLabors.ImageSharp.Advanced;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.Primitives;

namespace SixLabors.ImageSharp.Processing.Processors
{
    internal static class ImageProcessorExtensions
    {
        public static void Apply(this IImageProcessor processor, Image source, Rectangle sourceRectangle)
        {
            source.AcceptVisitor(new ApplyVisitor(processor, sourceRectangle));
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
                using (IImageProcessor<TPixel> processorImpl = this.processor.CreatePixelSpecificProcessor(image, this.sourceRectangle))
                {
                    processorImpl.Apply();
                }
            }
        }
    }
}
