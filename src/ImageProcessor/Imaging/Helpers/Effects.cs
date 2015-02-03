// --------------------------------------------------------------------------------------------------------------------
// <copyright file="Effects.cs" company="James South">
//   Copyright (c) James South.
//   Licensed under the Apache License, Version 2.0.
// </copyright>
// <summary>
//   Provides reusable effect methods to apply to images.
// </summary>
// --------------------------------------------------------------------------------------------------------------------

namespace ImageProcessor.Imaging.Helpers
{
    using System;
    using System.Drawing;
    using System.Drawing.Drawing2D;
    using System.Threading.Tasks;

    /// <summary>
    /// Provides reusable effect methods to apply to images.
    /// </summary>
    public static class Effects
    {
        /// <summary>
        /// Adds a vignette effect to the source image based on the given color.
        /// </summary>
        /// <param name="source">
        /// The <see cref="Image"/> source.
        /// </param>
        /// <param name="baseColor">
        /// <see cref="Color"/> to base the vignette on.
        /// </param>
        /// <param name="rectangle">
        /// The rectangle to define the bounds of the area to vignette. If null then the effect is applied
        /// to the entire image.
        /// </param>
        /// <param name="invert">
        /// Whether to invert the vignette.
        /// </param>
        /// <returns>
        /// The <see cref="Bitmap"/> with the vignette applied.
        /// </returns>
        public static Bitmap Vignette(Image source, Color baseColor, Rectangle? rectangle = null, bool invert = false)
        {
            using (Graphics graphics = Graphics.FromImage(source))
            {
                Rectangle bounds = rectangle ?? new Rectangle(0, 0, source.Width, source.Height);
                Rectangle ellipsebounds = bounds;

                // Increase the rectangle size by the difference between the rectangle dimensions and sqrt(2)/2 * the rectangle dimensions.
                // Why sqrt(2)/2? Because the point (sqrt(2)/2, sqrt(2)/2) is the 45 degree angle point on a unit circle. Scaling by the width 
                // and height gives the distance needed to inflate the rectangle to make sure it's fully covered.
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
                        Color centerColor;
                        Color edgeColor;
                        if (invert)
                        {
                            centerColor = Color.FromArgb(50, baseColor.R, baseColor.G, baseColor.B);
                            edgeColor = Color.FromArgb(0, baseColor.R, baseColor.G, baseColor.B);
                        }
                        else
                        {
                            centerColor = Color.FromArgb(0, baseColor.R, baseColor.G, baseColor.B);
                            edgeColor = Color.FromArgb(255, baseColor.R, baseColor.G, baseColor.B);
                        }

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
                }
            }

            return (Bitmap)source;
        }

        /// <summary>
        /// Adds a diffused glow (inverted vignette) effect to the source image based on the given color.
        /// </summary>
        /// <param name="source">The <see cref="Image"/> source.</param>
        /// <param name="baseColor"><see cref="Color"/> to base the vignette on.</param>
        /// <param name="rectangle">The rectangle to define the bounds of the area to vignette. If null then the effect is applied
        /// to the entire image.</param>
        /// <returns>The <see cref="Bitmap"/> with the vignette applied.</returns>
        public static Bitmap Glow(Image source, Color baseColor, Rectangle? rectangle = null)
        {
            return Vignette(source, baseColor, rectangle, true);
        }

        /// <summary>
        /// Applies the given image mask to the source.
        /// </summary>
        /// <param name="source">
        /// The source <see cref="Image"/>.
        /// </param>
        /// <param name="mask">
        /// The mask <see cref="Image"/>.
        /// </param>
        /// <exception cref="ArgumentException">
        /// Thrown if the two images are of different size.
        /// </exception>
        /// <returns>
        /// The masked <see cref="Bitmap"/>.
        /// </returns>
        public static Bitmap ApplyMask(Image source, Image mask)
        {
            if (mask.Size != source.Size)
            {
                throw new ArgumentException();
            }

            int width = mask.Width;
            int height = mask.Height;

            Bitmap toMask = new Bitmap(source);

            // Loop through and replace the alpha channel
            using (FastBitmap maskBitmap = new FastBitmap(mask))
            {
                using (FastBitmap sourceBitmap = new FastBitmap(toMask))
                {
                    Parallel.For(
                        0,
                        height,
                        y =>
                        {
                            for (int x = 0; x < width; x++)
                            {
                                // ReSharper disable AccessToDisposedClosure
                                Color maskColor = maskBitmap.GetPixel(x, y);
                                Color sourceColor = sourceBitmap.GetPixel(x, y);

                                if (sourceColor.A != 0)
                                {
                                    sourceBitmap.SetPixel(x, y, Color.FromArgb(maskColor.A, sourceColor.R, sourceColor.G, sourceColor.B));
                                }

                                // ReSharper restore AccessToDisposedClosure
                            }
                        });
                }
            }

            // Ensure the background is cleared out on non alpha supporting formats.
            Bitmap clear = new Bitmap(width, height);
            clear.SetResolution(source.HorizontalResolution, source.VerticalResolution);
            using (Graphics graphics = Graphics.FromImage(clear))
            {
                graphics.Clear(Color.Transparent);
                graphics.DrawImage(toMask, 0, 0, width, height);
            }

            toMask.Dispose();
            return clear;
        }
    }
}
