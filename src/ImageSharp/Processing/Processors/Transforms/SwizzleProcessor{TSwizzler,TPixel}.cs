// Copyright (c) Six Labors.
// Licensed under the Apache License, Version 2.0.

using System;
using SixLabors.ImageSharp.PixelFormats;

namespace SixLabors.ImageSharp.Processing.Processors.Transforms
{
    internal class SwizzleProcessor<TSwizzler, TPixel> : TransformProcessor<TPixel>
        where TSwizzler : struct, ISwizzler
        where TPixel : unmanaged, IPixel<TPixel>
    {
        private readonly TSwizzler swizzler;
        private readonly Size destinationSize;

        public SwizzleProcessor(Configuration configuration, TSwizzler swizzler, Image<TPixel> source, Rectangle sourceRectangle)
            : base(configuration, source, sourceRectangle)
        {
            this.swizzler = swizzler;
            this.destinationSize = swizzler.DestinationSize;
        }

        protected override Size GetDestinationSize()
            => this.destinationSize;

        protected override void OnFrameApply(ImageFrame<TPixel> source, ImageFrame<TPixel> destination)
        {
            Point p = default;
            Point newPoint;
            for (p.Y = 0; p.Y < source.Height; p.Y++)
            {
                Span<TPixel> rowSpan = source.GetPixelRowSpan(p.Y);
                for (p.X = 0; p.X < source.Width; p.X++)
                {
                    newPoint = this.swizzler.Transform(p);
                    destination[newPoint.X, newPoint.Y] = rowSpan[p.X];
                }
            }
        }
    }
}
