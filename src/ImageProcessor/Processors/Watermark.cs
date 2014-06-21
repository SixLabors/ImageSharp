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
    using ImageProcessor.Imaging;

    /// <summary>
    /// Encapsulates methods to add a watermark text overlay to an image.
    /// </summary>
    public class Watermark : IGraphicsProcessor
    {
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
                int opacity = textLayer.Opacity;
                int fontSize = textLayer.FontSize;
                FontStyle fontStyle = textLayer.Style;

                using (Graphics graphics = Graphics.FromImage(newImage))
                {
                    using (Font font = this.GetFont(textLayer.Font, fontSize, fontStyle))
                    {
                        using (StringFormat drawFormat = new StringFormat())
                        {
                            using (Brush brush = new SolidBrush(Color.FromArgb(opacity, textLayer.TextColor)))
                            {
                                Point origin = textLayer.Position;

                                // Work out the size of the text.
                                SizeF textSize = graphics.MeasureString(text, font, new SizeF(image.Width, image.Height), drawFormat);

                                // We need to ensure that there is a position set for the watermark
                                if (origin == Point.Empty)
                                {
                                    int x = (int)(image.Width - textSize.Width) / 2;
                                    int y = (int)(image.Height - textSize.Height) / 2;
                                    origin = new Point(x, y);
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
                                        Point shadowPoint = new Point(origin.X + shadowDiff, origin.Y + shadowDiff);

                                        // Set the bounds so any overlapping text will wrap.
                                        bounds = new RectangleF(shadowPoint, new SizeF(image.Width - shadowPoint.X, image.Height - shadowPoint.Y));

                                        graphics.DrawString(text, font, shadowBrush, bounds, drawFormat);
                                    }
                                }

                                // Set the bounds so any overlapping text will wrap.
                                bounds = new RectangleF(origin, new SizeF(image.Width - origin.X, image.Height - origin.Y));

                                graphics.DrawString(text, font, brush, bounds, drawFormat);
                            }
                        }
                    }

                    image.Dispose();
                    image = newImage;
                }
            }
            catch
            {
                if (newImage != null)
                {
                    newImage.Dispose();
                }
            }

            return image;
        }

        /// <summary>
        /// Returns the correct <see cref="T:System.Drawing.Font"/> for the given parameters.
        /// </summary>
        /// <param name="font">
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
        private Font GetFont(string font, int fontSize, FontStyle fontStyle)
        {
            try
            {
                using (FontFamily fontFamily = new FontFamily(font))
                {
                    return new Font(fontFamily, fontSize, fontStyle, GraphicsUnit.Pixel);
                }
            }
            catch
            {
                using (FontFamily fontFamily = FontFamily.GenericSansSerif)
                {
                    return new Font(fontFamily, fontSize, fontStyle, GraphicsUnit.Pixel);
                }
            }
        }
    }
}
