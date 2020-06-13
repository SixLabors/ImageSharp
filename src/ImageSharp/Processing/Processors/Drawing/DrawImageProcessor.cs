// Copyright (c) Six Labors.
// Licensed under the Apache License, Version 2.0.

using SixLabors.ImageSharp.Advanced;
using SixLabors.ImageSharp.PixelFormats;

namespace SixLabors.ImageSharp.Processing.Processors.Drawing
{
    /// <summary>
    /// Combines two images together by blending the pixels.
    /// </summary>
    public class DrawImageProcessor : IImageProcessor
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="DrawImageProcessor"/> class.
        /// </summary>
        /// <param name="image">The image to blend.</param>
        /// <param name="location">The location to draw the blended image.</param>
        /// <param name="colorBlendingMode">The blending mode to use when drawing the image.</param>
        /// <param name="alphaCompositionMode">The Alpha blending mode to use when drawing the image.</param>
        /// <param name="opacity">The opacity of the image to blend.</param>
        public DrawImageProcessor(
            Image image,
            Point location,
            PixelColorBlendingMode colorBlendingMode,
            PixelAlphaCompositionMode alphaCompositionMode,
            float opacity)
        {
            this.Image = image;
            this.Location = location;
            this.ColorBlendingMode = colorBlendingMode;
            this.AlphaCompositionMode = alphaCompositionMode;
            this.Opacity = opacity;
        }

        /// <summary>
        /// Gets the image to blend.
        /// </summary>
        public Image Image { get; }

        /// <summary>
        /// Gets the location to draw the blended image.
        /// </summary>
        public Point Location { get; }

        /// <summary>
        /// Gets the blending mode to use when drawing the image.
        /// </summary>
        public PixelColorBlendingMode ColorBlendingMode { get; }

        /// <summary>
        /// Gets the Alpha blending mode to use when drawing the image.
        /// </summary>
        public PixelAlphaCompositionMode AlphaCompositionMode { get; }

        /// <summary>
        /// Gets the opacity of the image to blend.
        /// </summary>
        public float Opacity { get; }

        /// <inheritdoc />
        public IImageProcessor<TPixelBg> CreatePixelSpecificProcessor<TPixelBg>(Configuration configuration, Image<TPixelBg> source, Rectangle sourceRectangle)
            where TPixelBg : unmanaged, IPixel<TPixelBg>
        {
            var visitor = new ProcessorFactoryVisitor<TPixelBg>(configuration, this, source, sourceRectangle);
            this.Image.AcceptVisitor(visitor);
            return visitor.Result;
        }

        private class ProcessorFactoryVisitor<TPixelBg> : IImageVisitor
            where TPixelBg : unmanaged, IPixel<TPixelBg>
        {
            private readonly Configuration configuration;
            private readonly DrawImageProcessor definition;
            private readonly Image<TPixelBg> source;
            private readonly Rectangle sourceRectangle;

            public ProcessorFactoryVisitor(Configuration configuration, DrawImageProcessor definition, Image<TPixelBg> source, Rectangle sourceRectangle)
            {
                this.configuration = configuration;
                this.definition = definition;
                this.source = source;
                this.sourceRectangle = sourceRectangle;
            }

            public IImageProcessor<TPixelBg> Result { get; private set; }

            public void Visit<TPixelFg>(Image<TPixelFg> image)
                where TPixelFg : unmanaged, IPixel<TPixelFg>
            {
                this.Result = new DrawImageProcessor<TPixelBg, TPixelFg>(
                    this.configuration,
                    image,
                    this.source,
                    this.sourceRectangle,
                    this.definition.Location,
                    this.definition.ColorBlendingMode,
                    this.definition.AlphaCompositionMode,
                    this.definition.Opacity);
            }
        }
    }
}
