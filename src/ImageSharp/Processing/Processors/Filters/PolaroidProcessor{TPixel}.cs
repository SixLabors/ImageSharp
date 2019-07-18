// Copyright (c) Six Labors and contributors.
// Licensed under the Apache License, Version 2.0.

using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing.Processors.Overlays;
using SixLabors.Primitives;

namespace SixLabors.ImageSharp.Processing.Processors.Filters
{
    /// <summary>
    /// Converts the colors of the image recreating an old Polaroid effect.
    /// </summary>
    internal class PolaroidProcessor<TPixel> : FilterProcessor<TPixel>
        where TPixel : struct, IPixel<TPixel>
    {
        private static readonly Color LightOrange = Color.FromRgba(255, 153, 102, 128);

        private static readonly Color VeryDarkOrange = Color.FromRgb(102, 34, 0);

        /// <summary>
        /// Initializes a new instance of the <see cref="PolaroidProcessor{TPixel}"/> class.
        /// </summary>
        /// <param name="definition">The <see cref="PolaroidProcessor"/> defining the parameters.</param>
        public PolaroidProcessor(PolaroidProcessor definition)
            : base(definition)
        {
        }

        /// <inheritdoc/>
        protected override void AfterFrameApply(
            ImageFrame<TPixel> source,
            Rectangle sourceRectangle,
            Configuration configuration)
        {
            new VignetteProcessor(VeryDarkOrange).Apply(source, sourceRectangle, configuration);
            new GlowProcessor(LightOrange, source.Width / 4F).Apply(source, sourceRectangle, configuration);
        }
    }
}