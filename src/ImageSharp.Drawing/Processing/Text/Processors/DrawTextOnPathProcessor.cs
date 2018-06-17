// Copyright (c) Six Labors and contributors.
// Licensed under the Apache License, Version 2.0.

using System;
using System.Threading.Tasks;
using SixLabors.Fonts;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Primitives;
using SixLabors.ImageSharp.Processing.Drawing.Brushes;
using SixLabors.ImageSharp.Processing.Drawing.Pens;
using SixLabors.ImageSharp.Processing.Drawing.Processors;
using SixLabors.ImageSharp.Processing.Processors;
using SixLabors.Primitives;
using SixLabors.Shapes;

namespace SixLabors.ImageSharp.Processing.Text.Processors
{
    /// <summary>
    /// Using the brush as a source of pixels colors blends the brush color with source.
    /// </summary>
    /// <typeparam name="TPixel">The pixel format.</typeparam>
    internal class DrawTextOnPathProcessor<TPixel> : ImageProcessor<TPixel>
        where TPixel : struct, IPixel<TPixel>
    {
        private FillRegionProcessor<TPixel> fillRegionProcessor = null;

        /// <summary>
        /// Initializes a new instance of the <see cref="DrawTextOnPathProcessor{TPixel}"/> class.
        /// </summary>
        /// <param name="options">The options</param>
        /// <param name="text">The text we want to render</param>
        /// <param name="font">The font we want to render with</param>
        /// <param name="brush">The brush to source pixel colors from.</param>
        /// <param name="pen">The pen to outline text with.</param>
        /// <param name="path">The path on which to draw the text along.</param>
        public DrawTextOnPathProcessor(TextGraphicsOptions options, string text, Font font, IBrush<TPixel> brush, IPen<TPixel> pen, IPath path)
        {
            this.Brush = brush;
            this.Options = options;
            this.Text = text;
            this.Pen = pen;
            this.Font = font;
            this.Path = path;
        }

        /// <summary>
        /// Gets or sets the brush.
        /// </summary>
        public IBrush<TPixel> Brush { get; set; }

        /// <summary>
        /// Gets or sets the options
        /// </summary>
        private TextGraphicsOptions Options { get; set; }

        /// <summary>
        /// Gets or sets the text
        /// </summary>
        private string Text { get; set; }

        /// <summary>
        /// Gets or sets the pen used for outlining the text, if Null then we will not outline
        /// </summary>
        public IPen<TPixel> Pen { get; set; }

        /// <summary>
        /// Gets or sets the font used to render the text.
        /// </summary>
        public Font Font { get; set; }

        /// <summary>
        /// Gets or sets the path to draw the text along.
        /// </summary>
        public IPath Path { get; set; }

        protected override void BeforeImageApply(Image<TPixel> source, Rectangle sourceRectangle)
        {
            base.BeforeImageApply(source, sourceRectangle);

            // do everythign at the image level as we are deligating the processing down to other processors
            var style = new RendererOptions(this.Font, this.Options.DpiX, this.Options.DpiY)
            {
                ApplyKerning = this.Options.ApplyKerning,
                TabWidth = this.Options.TabWidth,
                WrappingWidth = this.Path.Length,
                HorizontalAlignment = this.Options.HorizontalAlignment,
                VerticalAlignment = this.Options.VerticalAlignment
            };

            IPathCollection glyphs = TextBuilder.GenerateGlyphs(this.Text, this.Path, style);
            this.fillRegionProcessor = new FillRegionProcessor<TPixel>();
            this.fillRegionProcessor.Options = (GraphicsOptions)this.Options;

            if (this.Brush != null)
            {
                this.fillRegionProcessor.Brush = this.Brush;

                foreach (IPath p in glyphs)
                {
                    this.fillRegionProcessor.Region = new ShapeRegion(p);
                    this.fillRegionProcessor.Apply(source, sourceRectangle);
                }
            }

            if (this.Pen != null)
            {
                this.fillRegionProcessor.Brush = this.Pen.StrokeFill;

                foreach (IPath p in glyphs)
                {
                    this.fillRegionProcessor.Region = new ShapePath(p, this.Pen);
                    this.fillRegionProcessor.Apply(source, sourceRectangle);
                }
            }
        }

        /// <inheritdoc/>
        protected override void OnFrameApply(ImageFrame<TPixel> source, Rectangle sourceRectangle, Configuration configuration)
        {
            // this is a no-op as we have processes all as an image, we should be able to pass out of before email apply a skip frames outcome
        }
    }
}