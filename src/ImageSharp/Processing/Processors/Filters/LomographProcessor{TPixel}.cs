// Copyright (c) Six Labors and contributors.
// Licensed under the Apache License, Version 2.0.

using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing.Processors.Overlays;
using SixLabors.Primitives;

namespace SixLabors.ImageSharp.Processing.Processors.Filters
{
    /// <summary>
    /// Converts the colors of the image recreating an old Lomograph effect.
    /// </summary>
    internal class LomographProcessor<TPixel> : FilterProcessor<TPixel>
        where TPixel : struct, IPixel<TPixel>
    {
        private static readonly TPixel VeryDarkGreen = ColorBuilder<TPixel>.FromRGBA(0, 10, 0, 255);

        public LomographProcessor(LomographProcessor definition)
            : base(definition)
        {
        }

        /// <inheritdoc/>
        protected override void AfterFrameApply(ImageFrame<TPixel> source, Rectangle sourceRectangle, Configuration configuration)
        {
            new VignetteProcessor<TPixel>(VeryDarkGreen).Apply(source, sourceRectangle, configuration);
        }
    }
}