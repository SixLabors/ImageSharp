// Copyright (c) Six Labors and contributors.
// Licensed under the Apache License, Version 2.0.

using SixLabors.ImageSharp.Memory;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.Primitives;

namespace SixLabors.ImageSharp.Processing.Processors
{
    /// <summary>
    /// Converts the colors of the image recreating an old Polaroid effect.
    /// </summary>
    /// <typeparam name="TPixel">The pixel format.</typeparam>
    internal class PolaroidProcessor<TPixel> : FilterProcessor<TPixel>
        where TPixel : struct, IPixel<TPixel>
    {
        private static readonly TPixel VeryDarkOrange = ColorBuilder<TPixel>.FromRGB(102, 34, 0);
        private static readonly TPixel LightOrange = ColorBuilder<TPixel>.FromRGBA(255, 153, 102, 128);

        private readonly MemoryManager memoryManager;

        private readonly GraphicsOptions options;

        /// <summary>
        /// Initializes a new instance of the <see cref="PolaroidProcessor{TPixel}" /> class.
        /// </summary>
        /// <param name="memoryManager">The <see cref="MemoryManager"/> to use for buffer allocations.</param>
        /// <param name="options">The options effecting blending and composition.</param>
        public PolaroidProcessor(MemoryManager memoryManager, GraphicsOptions options)
            : base(MatrixFilters.PolaroidFilter)
        {
            this.memoryManager = memoryManager;
            this.options = options;
        }

        /// <inheritdoc/>
        protected override void AfterApply(ImageFrame<TPixel> source, Rectangle sourceRectangle, Configuration configuration)
        {
            new VignetteProcessor<TPixel>(this.memoryManager, VeryDarkOrange, this.options).Apply(source, sourceRectangle, configuration);
            new GlowProcessor<TPixel>(this.memoryManager, LightOrange, source.Width / 4F, this.options).Apply(source, sourceRectangle, configuration);
        }
    }
}