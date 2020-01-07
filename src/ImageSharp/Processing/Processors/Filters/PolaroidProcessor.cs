// Copyright (c) Six Labors and contributors.
// Licensed under the Apache License, Version 2.0.

using SixLabors.Primitives;

namespace SixLabors.ImageSharp.Processing.Processors.Filters
{
    /// <summary>
    /// Converts the colors of the image recreating an old Polaroid effect.
    /// </summary>
    public sealed class PolaroidProcessor : FilterProcessor
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="PolaroidProcessor" /> class.
        /// </summary>
        public PolaroidProcessor()
            : base(KnownFilterMatrices.PolaroidFilter)
        {
        }

        /// <inheritdoc />
        public override IImageProcessor<TPixel> CreatePixelSpecificProcessor<TPixel>(Configuration configuration, Image<TPixel> source, Rectangle sourceRectangle) =>
            new PolaroidProcessor<TPixel>(configuration, this, source, sourceRectangle);
    }
}
