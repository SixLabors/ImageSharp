// Copyright (c) Six Labors and contributors.
// Licensed under the Apache License, Version 2.0.

using System;
using System.Numerics;

using SixLabors.ImageSharp.PixelFormats;

namespace SixLabors.ImageSharp.Processing.Processors.Effects
{
    /// <summary>
    /// Applies a user defined row processing delegate to the image.
    /// </summary>
    /// <typeparam name="TPixel">The pixel format.</typeparam>
    internal sealed class PixelRowDelegateProcessor<TPixel> : PixelRowDelegateProcessorBase<TPixel>
        where TPixel : struct, IPixel<TPixel>
    {
        /// <summary>
        /// The user defined pixel row processing delegate.
        /// </summary>
        private readonly PixelRowOperation pixelRowOperation;

        /// <summary>
        /// Initializes a new instance of the <see cref="PixelRowDelegateProcessor{TPixel}"/> class.
        /// </summary>
        /// <param name="configuration">The configuration which allows altering default behaviour or extending the library.</param>
        /// <param name="definition">The <see cref="PixelRowDelegateProcessor"/> defining the processor parameters.</param>
        /// <param name="source">The source <see cref="Image{TPixel}"/> for the current processor instance.</param>
        /// <param name="sourceRectangle">The source area to process for the current processor instance.</param>
        public PixelRowDelegateProcessor(Configuration configuration, PixelRowDelegateProcessor definition, Image<TPixel> source, Rectangle sourceRectangle)
            : base(configuration, definition.Modifiers, source, sourceRectangle)
        {
            this.pixelRowOperation = definition.PixelRowOperation;
        }

        /// <inheritdoc/>
        protected override void ApplyPixelRowDelegate(Span<Vector4> span, Point offset) => this.pixelRowOperation(span);
    }
}
