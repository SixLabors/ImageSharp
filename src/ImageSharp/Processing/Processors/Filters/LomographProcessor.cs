// Copyright (c) Six Labors and contributors.
// Licensed under the Apache License, Version 2.0.

using SixLabors.Primitives;

namespace SixLabors.ImageSharp.Processing.Processors.Filters
{
    /// <summary>
    /// Converts the colors of the image recreating an old Lomograph effect.
    /// </summary>
    public sealed class LomographProcessor : FilterProcessor
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="LomographProcessor" /> class.
        /// </summary>
        public LomographProcessor()
            : base(KnownFilterMatrices.LomographFilter)
        {
        }

        /// <inheritdoc />
        public override IImageProcessor<TPixel> CreatePixelSpecificProcessor<TPixel>(Configuration configuration, Image<TPixel> source, Rectangle sourceRectangle) =>
            new LomographProcessor<TPixel>(configuration, this, source, sourceRectangle);
    }
}
