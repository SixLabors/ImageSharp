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
        private static readonly Color VeryDarkGreen = Color.FromRgba(0, 10, 0, 255);

        /// <summary>
        /// Initializes a new instance of the <see cref="LomographProcessor{TPixel}"/> class.
        /// </summary>
        /// <param name="configuration">The configuration which allows altering default behaviour or extending the library.</param>
        /// <param name="definition">The <see cref="LomographProcessor"/> defining the parameters.</param>
        /// <param name="source">The source <see cref="Image{TPixel}"/> for the current processor instance.</param>
        /// <param name="sourceRectangle">The source area to process for the current processor instance.</param>
        public LomographProcessor(Configuration configuration, LomographProcessor definition, Image<TPixel> source, Rectangle sourceRectangle)
            : base(configuration, definition, source, sourceRectangle)
        {
        }

        /// <inheritdoc/>
        protected override void AfterImageApply()
        {
            new VignetteProcessor(VeryDarkGreen).Execute(this.Configuration, this.Source, this.SourceRectangle);
            base.AfterImageApply();
        }
    }
}
