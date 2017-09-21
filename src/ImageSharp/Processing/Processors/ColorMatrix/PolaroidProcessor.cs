// Copyright (c) Six Labors and contributors.
// Licensed under the Apache License, Version 2.0.

using System.Numerics;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.Primitives;

namespace SixLabors.ImageSharp.Processing.Processors
{
    /// <summary>
    /// Converts the colors of the image recreating an old Polaroid effect.
    /// </summary>
    /// <typeparam name="TPixel">The pixel format.</typeparam>
    internal class PolaroidProcessor<TPixel> : ColorMatrixProcessor<TPixel>
        where TPixel : struct, IPixel<TPixel>
    {
        private static readonly TPixel VeryDarkOrange = ColorBuilder<TPixel>.FromRGB(102, 34, 0);
        private static readonly TPixel LightOrange = ColorBuilder<TPixel>.FromRGBA(255, 153, 102, 178);
        private readonly GraphicsOptions options;

        /// <summary>
        /// Initializes a new instance of the <see cref="PolaroidProcessor{TPixel}" /> class.
        /// </summary>
        /// <param name="options">The options effecting blending and composition.</param>
        public PolaroidProcessor(GraphicsOptions options)
        {
            this.options = options;
        }

        /// <inheritdoc/>
        public override Matrix4x4 Matrix => new Matrix4x4
        {
            M11 = 1.538F,
            M12 = -0.062F,
            M13 = -0.262F,
            M21 = -0.022F,
            M22 = 1.578F,
            M23 = -0.022F,
            M31 = .216F,
            M32 = -.16F,
            M33 = 1.5831F,
            M41 = 0.02F,
            M42 = -0.05F,
            M43 = -0.05F,
            M44 = 1
        };

        /// <inheritdoc/>
        protected override void AfterApply(ImageFrame<TPixel> source, Rectangle sourceRectangle, Configuration configuration)
        {
            new VignetteProcessor<TPixel>(VeryDarkOrange, this.options).Apply(source, sourceRectangle, configuration);
            new GlowProcessor<TPixel>(LightOrange, source.Width / 4F, this.options).Apply(source, sourceRectangle, configuration);
        }
    }
}