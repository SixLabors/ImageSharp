// --------------------------------------------------------------------------------------------------------------------
// <copyright file="Watermark.cs" company="James South">
//   Copyright (c) James South.
//   Licensed under the Apache License, Version 2.0.
// </copyright>
// <summary>
//   Encapsulates methods to add a watermark text overlay to an image.
// </summary>
// --------------------------------------------------------------------------------------------------------------------

namespace ImageProcessor.Processors
{
    using System;
    using System.Collections.Generic;
    using System.Drawing;
    using System.Drawing.Text;

    using ImageProcessor.Common.Exceptions;
    using ImageProcessor.Imaging;

    /// <summary>
    /// Encapsulates methods to add a watermark text overlay to an image.
    /// </summary>
    public class Watermark : IGraphicsProcessor
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="Watermark"/> class.
        /// </summary>
        public Watermark()
        {
            this.Settings = new Dictionary<string, string>();
        }

        /// <summary>
        /// Gets or sets DynamicParameter.
        /// </summary>
        public dynamic DynamicParameter
        {
            get;
            set;
        }

        /// <summary>
        /// Gets or sets any additional settings required by the processor.
        /// </summary>
        public Dictionary<string, string> Settings
        {
            get;
            set;
        }

        /// <summary>
        /// Processes the image.
        /// </summary>
        /// <param name="factory">
        /// The current instance of the <see cref="T:ImageProcessor.ImageFactory"/> class containing
        /// the image to process.
        /// </param>
        /// <returns>
        /// The processed image from the current instance of the <see cref="T:ImageProcessor.ImageFactory"/> class.
        /// </returns>
        public Image ProcessImage(ImageFactory factory)
        {
            Bitmap newImage = null;
            Image image = factory.Image;

            try
            {
                newImage = new Bitmap(image);
                TextLayer textLayer = this.DynamicParameter;
                string text = textLayer.Text;
                int opacity = Math.Min((int)Math.Ceiling((textLayer.Opacity / 100f) * 255), 255);
                int fontSize = textLayer.FontSize;
                FontStyle fontStyle = textLayer.Style;
                bool fallbackUsed = false;

                using (Graphics graphics = Graphics.FromImage(newImage))
                {
                    using (Font font = this.GetFont(textLayer.FontFamily, fontSize, fontStyle))
                    {
                        using (StringFormat drawFormat = new StringFormat())
                        {
                            StringFormatFlags? formatFlags = this.GetFlags(textLayer);
                            if (formatFlags != null)
                            {
                                drawFormat.FormatFlags = formatFlags.Value;
                            }

                            using (Brush brush = new SolidBrush(Color.FromArgb(opacity, textLayer.FontColor)))
                            {
                                Point? origin = textLayer.Position;

                                // Work out the size of the text.
                                SizeF textSize = graphics.MeasureString(text, font, new SizeF(image.Width, image.Height), drawFormat);

                                // We need to ensure that there is a position set for the watermark
                                if (origin == null)
                                {
                                    int x = textLayer.RightToLeft
                                        ? 0
                                        : (int)(image.Width - textSize.Width) / 2;
                                    int y = (int)(image.Height - textSize.Height) / 2;
                                    origin = new Point(x, y);

                                    fallbackUsed = true;
                                }

                                // Set the hinting and draw the text.
                                graphics.TextRenderingHint = TextRenderingHint.AntiAliasGridFit;

                                // Create bounds for the text.
                                RectangleF bounds;

                                if (textLayer.DropShadow)
                                {
                                    // Shadow opacity should change with the base opacity.
                                    int shadowOpacity = opacity - (int)Math.Ceiling((30 / 100f) * 255);
                                    int finalShadowOpacity = shadowOpacity > 0 ? shadowOpacity : 0;

                                    using (Brush shadowBrush = new SolidBrush(Color.FromArgb(finalShadowOpacity, Color.Black)))
                                    {
                                        // Scale the shadow position to match the font size.
                                        // Magic number but it's based on artistic preference.
                                        int shadowDiff = (int)Math.Ceiling(fontSize / 24f);
                                        Point shadowPoint = new Point(origin.Value.X + shadowDiff, origin.Value.Y + shadowDiff);

                                        // Set the bounds so any overlapping text will wrap.
                                        if (textLayer.RightToLeft && fallbackUsed)
                                        {
                                            bounds = new RectangleF(shadowPoint, new SizeF(image.Width - ((int)(image.Width - textSize.Width) / 2) - shadowPoint.X, image.Height - shadowPoint.Y));
                                        }
                                        else
                                        {
                                            bounds = new RectangleF(shadowPoint, new SizeF(image.Width - shadowPoint.X, image.Height - shadowPoint.Y));
                                        }

                                        graphics.DrawString(text, font, shadowBrush, bounds, drawFormat);
                                    }
                                }

                                // Set the bounds so any overlapping text will wrap.
                                if (textLayer.RightToLeft && fallbackUsed)
                                {
                                    bounds = new RectangleF(origin.Value, new SizeF(image.Width - ((int)(image.Width - textSize.Width) / 2), image.Height - origin.Value.Y));
                                }
                                else
                                {
                                    bounds = new RectangleF(origin.Value, new SizeF(image.Width - origin.Value.X, image.Height - origin.Value.Y));
                                }

                                graphics.DrawString(text, font, brush, bounds, drawFormat);
                            }
                        }
                    }

                    image.Dispose();
                    image = newImage;
                }
            }
            catch (Exception ex)
            {
                if (newImage != null)
                {
                    newImage.Dispose();
                }

                throw new ImageProcessingException("Error processing image with " + this.GetType().Name, ex);
            }

            return image;
        }

        /// <summary>
        /// Returns the correct <see cref="T:System.Drawing.Font"/> for the given parameters.
        /// </summary>
        /// <param name="fontFamily">
        /// The name of the font.
        /// </param>
        /// <param name="fontSize">
        /// The font size.
        /// </param>
        /// <param name="fontStyle">
        /// The <see cref="T:System.Drawing.FontStyle"/> style.
        /// </param>
        /// <returns>
        /// The correct <see cref="T:System.Drawing.Font"/>
        /// </returns>
        private Font GetFont(FontFamily fontFamily, int fontSize, FontStyle fontStyle)
        {
            try
            {
                using (fontFamily)
                {
                    return new Font(fontFamily, fontSize, fontStyle, GraphicsUnit.Pixel);
                }
            }
            catch
            {
                using (FontFamily genericFontFamily = FontFamily.GenericSansSerif)
                {
                    return new Font(genericFontFamily, fontSize, fontStyle, GraphicsUnit.Pixel);
                }
            }
        }

        /// <summary>
        /// Returns the correct flags for the given text layer.
        /// </summary>
        /// <param name="textLayer">
        /// The <see cref="TextLayer"/> to return the flags for.
        /// </param>
        /// <returns>
        /// The <see cref="StringFormatFlags"/>.
        /// </returns>
        private StringFormatFlags? GetFlags(TextLayer textLayer)
        {
            if (textLayer.Vertical && textLayer.RightToLeft)
            {
                return StringFormatFlags.DirectionVertical | StringFormatFlags.DirectionRightToLeft;
            }

            if (textLayer.Vertical)
            {
                return StringFormatFlags.DirectionVertical;
            }

            if (textLayer.RightToLeft)
            {
                return StringFormatFlags.DirectionRightToLeft;
            }

            return null;
        }
    }
}