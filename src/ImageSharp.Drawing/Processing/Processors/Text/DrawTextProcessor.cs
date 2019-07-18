// Copyright (c) Six Labors and contributors.
// Licensed under the Apache License, Version 2.0.

using System;

using SixLabors.Fonts;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.Primitives;

namespace SixLabors.ImageSharp.Processing.Processors.Text
{
    /// <summary>
    /// Defines a processor to draw text on an <see cref="Image"/>.
    /// </summary>
    public class DrawTextProcessor : IImageProcessor
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="DrawTextProcessor"/> class.
        /// </summary>
        /// <param name="options">The options</param>
        /// <param name="text">The text we want to render</param>
        /// <param name="font">The font we want to render with</param>
        /// <param name="brush">The brush to source pixel colors from.</param>
        /// <param name="pen">The pen to outline text with.</param>
        /// <param name="location">The location on the image to start drawing the text from.</param>
        public DrawTextProcessor(TextGraphicsOptions options, string text, Font font, IBrush brush, IPen pen, PointF location)
        {
            Guard.NotNull(text, nameof(text));
            Guard.NotNull(font, nameof(font));

            if (brush is null && pen is null)
            {
                throw new ArgumentNullException($"Expected a {nameof(brush)} or {nameof(pen)}. Both were null");
            }

            this.Options = options;
            this.Text = text;
            this.Font = font;
            this.Location = location;
            this.Brush = brush;
            this.Pen = pen;
        }

        /// <summary>
        /// Gets the brush used to fill the glyphs.
        /// </summary>
        public IBrush Brush { get; }

        /// <summary>
        /// Gets the <see cref="TextGraphicsOptions"/> defining blending modes and text-specific drawing settings.
        /// </summary>
        public TextGraphicsOptions Options { get; }

        /// <summary>
        /// Gets the text to draw.
        /// </summary>
        public string Text { get; }

        /// <summary>
        /// Gets the pen used for outlining the text, if Null then we will not outline
        /// </summary>
        public IPen Pen { get; }

        /// <summary>
        /// Gets the font used to render the text.
        /// </summary>
        public Font Font { get; }

        /// <summary>
        /// Gets the location to draw the text at.
        /// </summary>
        public PointF Location { get; }

        /// <inheritdoc />
        public IImageProcessor<TPixel> CreatePixelSpecificProcessor<TPixel>()
            where TPixel : struct, IPixel<TPixel>
        {
            return new DrawTextProcessor<TPixel>(this);
        }
    }
}