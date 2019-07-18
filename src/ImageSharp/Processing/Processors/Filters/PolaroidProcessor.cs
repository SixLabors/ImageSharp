// Copyright (c) Six Labors and contributors.
// Licensed under the Apache License, Version 2.0.

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
        public override IImageProcessor<TPixel> CreatePixelSpecificProcessor<TPixel>() =>
            new PolaroidProcessor<TPixel>(this);
    }
}