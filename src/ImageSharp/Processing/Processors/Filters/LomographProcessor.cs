// Copyright (c) Six Labors and contributors.
// Licensed under the Apache License, Version 2.0.

using System.Numerics;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.Primitives;

namespace SixLabors.ImageSharp.Processing.Processors
{
    /// <summary>
    /// Converts the colors of the image recreating an old Lomograph effect.
    /// </summary>
    /// <typeparam name="TPixel">The pixel format.</typeparam>
    internal class LomographProcessor<TPixel> : FilterProcessor<TPixel>
        where TPixel : struct, IPixel<TPixel>
    {
        private static readonly TPixel VeryDarkGreen = ColorBuilder<TPixel>.FromRGBA(0, 10, 0, 255);
        private readonly GraphicsOptions options;

        /// <summary>
        /// Initializes a new instance of the <see cref="LomographProcessor{TPixel}" /> class.
        /// </summary>
        /// <param name="options">The options effecting blending and composition.</param>
        public LomographProcessor(GraphicsOptions options)
            : base(MatrixFilters.LomographFilter)
        {
            this.options = options;
        }

        /// <inheritdoc/>
        protected override void AfterApply(ImageFrame<TPixel> source, Rectangle sourceRectangle, Configuration configuration)
        {
            new VignetteProcessor<TPixel>(VeryDarkGreen, this.options).Apply(source, sourceRectangle, configuration);
        }
    }
}