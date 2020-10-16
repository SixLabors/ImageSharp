// Copyright (c) Six Labors.
// Licensed under the Apache License, Version 2.0.

using SixLabors.ImageSharp.PixelFormats;

namespace SixLabors.ImageSharp.Processing.Processors.Transforms
{
    internal class SwizzleProcessor<TSwizzler, TPixel> : ImageProcessor<TPixel>
        where TSwizzler : struct, ISwizzler
        where TPixel : unmanaged, IPixel<TPixel>
    {
        private readonly TSwizzler swizzler;

        public SwizzleProcessor(Configuration configuration, TSwizzler swizzler, Image<TPixel> source, Rectangle sourceRectangle)
            : base(configuration, source, sourceRectangle)
        {
            this.swizzler = swizzler;
        }

        /// <inheritdoc/>
        protected override void OnFrameApply(ImageFrame<TPixel> source)
        {
            for (int y = 0; y < source.Height; y++)
            {
                var pixelRowSpan = source.GetPixelRowSpan(y);
                for (int x = 0; x < source.Width; x++)
                {
                    var newPoint = this.swizzler.Transform(new Point(x, y));
                }
            }
        }
    }
}
