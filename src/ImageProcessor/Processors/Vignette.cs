// --------------------------------------------------------------------------------------------------------------------
// <copyright file="Vignette.cs" company="James South">
//   Copyright (c) James South.
//   Licensed under the Apache License, Version 2.0.
// </copyright>
// <summary>
//   Encapsulates methods with which to add a vignette image effect to an image.
// </summary>
// --------------------------------------------------------------------------------------------------------------------

namespace ImageProcessor.Processors
{
    #region Using
    using System;
    using System.Collections.Generic;
    using System.Drawing;
    using System.Drawing.Drawing2D;
    using System.Text.RegularExpressions;
    #endregion

    /// <summary>
    /// Encapsulates methods with which to add a vignette image effect to an image.
    /// </summary>
    public class Vignette : IGraphicsProcessor
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="Vignette"/> class.
        /// </summary>
        public Vignette()
        {
            this.DynamicParameter = Color.Black;
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

                using (Graphics graphics = Graphics.FromImage(newImage))
                {
                    Rectangle bounds = new Rectangle(0, 0, newImage.Width, newImage.Height);
                    Rectangle ellipsebounds = bounds;

                    // Increase the rectangle size by the difference between the rectangle dimensions and sqrt(2)/2 * the rectangle dimensions.
                    // Why sqrt(2)/2? Because the point (sqrt(2)/2, sqrt(2)/2) is the 45 degree angle point on a unit circle. Scaling by the width 
                    // and height gives the distance needed to inflate the rect to make sure it's fully covered.
                    ellipsebounds.Offset(-ellipsebounds.X, -ellipsebounds.Y);
                    int x = ellipsebounds.Width - (int)Math.Floor(.70712 * ellipsebounds.Width);
                    int y = ellipsebounds.Height - (int)Math.Floor(.70712 * ellipsebounds.Height);
                    ellipsebounds.Inflate(x, y);

                    using (GraphicsPath path = new GraphicsPath())
                    {
                        path.AddEllipse(ellipsebounds);
                        using (PathGradientBrush brush = new PathGradientBrush(path))
                        {
                            // Fill a rectangle with an elliptical gradient brush that goes from transparent to opaque. 
                            // This has the effect of painting the far corners with the given color and shade less on the way in to the centre.
                            Color baseColor = (Color)this.DynamicParameter;
                            Color centerColor = Color.FromArgb(0, baseColor.R, baseColor.G, baseColor.B);
                            Color edgeColor = Color.FromArgb(255, baseColor.R, baseColor.G, baseColor.B);
                            
                            brush.WrapMode = WrapMode.Tile;
                            brush.CenterColor = centerColor;
                            brush.SurroundColors = new[] { edgeColor };

                            Blend blend = new Blend
                                {
                                    Positions = new[] { 0.0f, 0.2f, 0.4f, 0.6f, 0.8f, 1.0F },
                                    Factors = new[] { 0.0f, 0.5f, 1f, 1f, 1.0f, 1.0f }
                                };

                            brush.Blend = blend;

                            Region oldClip = graphics.Clip;
                            graphics.Clip = new Region(bounds);
                            graphics.FillRectangle(brush, ellipsebounds);
                            graphics.Clip = oldClip;
                        }

                        // Reassign the image.
                        image.Dispose();
                        image = newImage;
                    }
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
    }
}