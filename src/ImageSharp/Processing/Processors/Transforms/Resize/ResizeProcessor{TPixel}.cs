// Copyright (c) Six Labors and contributors.
// Licensed under the Apache License, Version 2.0.

using SixLabors.ImageSharp.PixelFormats;

namespace SixLabors.ImageSharp.Processing.Processors.Transforms
{
    /// <summary>
    /// Implements resizing of images using various resamplers.
    /// </summary>
    /// <typeparam name="TPixel">The pixel format.</typeparam>
    internal partial class ResizeProcessor<TPixel> : TransformProcessor<TPixel>, IResamplingImageProcessor<TPixel>
        where TPixel : struct, IPixel<TPixel>
    {
        private readonly int destinationWidth;
        private readonly int destinationHeight;
        private readonly IResampler resampler;
        private readonly Rectangle destinationRectangle;
        private readonly bool compand;
        private Image<TPixel> destination;

        public ResizeProcessor(Configuration configuration, ResizeProcessor definition, Image<TPixel> source, Rectangle sourceRectangle)
            : base(configuration, source, sourceRectangle)
        {
            this.destinationWidth = definition.DestinationWidth;
            this.destinationHeight = definition.DestinationHeight;
            this.destinationRectangle = definition.DestinationRectangle;
            this.resampler = definition.Sampler;
            this.compand = definition.Compand;
        }

        /// <inheritdoc/>
        protected override Size GetDestinationSize() => new Size(this.destinationWidth, this.destinationHeight);

        /// <inheritdoc/>
        protected override void BeforeImageApply(Image<TPixel> destination)
        {
            this.destination = destination;
            this.resampler.ApplyTransform(this);

            base.BeforeImageApply(destination);
        }

        /// <inheritdoc/>
        protected override void OnFrameApply(ImageFrame<TPixel> source, ImageFrame<TPixel> destination)
        {
            // Everything happens in BeforeImageApply.
        }

        public void ApplyTransform<TResampler>(in TResampler sampler)
            where TResampler : struct, IResampler =>
            ApplyResizeTransform(
                this.Configuration,
                in sampler,
                this.Source,
                this.destination,
                this.SourceRectangle,
                this.destinationRectangle,
                this.compand);
    }
}
