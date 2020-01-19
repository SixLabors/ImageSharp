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
        /// <param name="configuration">The configuration which allows altering default behaviour or extending the library.</param>
        /// <param name="definition">The <see cref="PolaroidProcessor"/> defining the parameters.</param>
        /// <param name="source">The source <see cref="Image{TPixel}"/> for the current processor instance.</param>
        /// <param name="sourceRectangle">The source area to process for the current processor instance.</param>
        public PolaroidProcessor(Configuration configuration, PolaroidProcessor definition, Image<TPixel> source, Rectangle sourceRectangle)
            : base(configuration, definition, source, sourceRectangle)
        {
        }

        /// <inheritdoc/>
        protected override void AfterImageApply()
        {
            new VignetteProcessor(VeryDarkOrange).Execute(this.Configuration, this.Source, this.SourceRectangle);
            new GlowProcessor(LightOrange, this.Source.Width / 4F).Execute(this.Configuration, this.Source, this.SourceRectangle);
            base.AfterImageApply();
        }
    }
}
