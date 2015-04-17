// --------------------------------------------------------------------------------------------------------------------
// <copyright file="HalftoneFilter.cs" company="James South">
//   Copyright (c) James South.
//   Licensed under the Apache License, Version 2.0.
// </copyright>
// <summary>
//   The halftone filter applies a classical CMYK filter to the given image.
// </summary>
// --------------------------------------------------------------------------------------------------------------------

namespace ImageProcessor.Imaging.Filters.Artistic
{
    using System;
    using System.Collections.Generic;
    using System.Drawing;
    using System.Drawing.Drawing2D;
    using System.Threading.Tasks;

    using ImageProcessor.Imaging.Colors;
    using ImageProcessor.Imaging.Helpers;

    /// <summary>
    /// The halftone filter applies a classical CMYK filter to the given image.
    /// </summary>
    public class HalftoneFilter
    {
        /// <summary>
        /// The angle of the cyan component in degrees.
        /// </summary>
        private float cyanAngle = 15f;

        /// <summary>
        /// The angle of the magenta component in degrees.
        /// </summary>
        private float magentaAngle = 75f;

        /// <summary>
        /// The angle of the yellow component in degrees.
        /// </summary>
        // ReSharper disable once RedundantDefaultMemberInitializer
        private float yellowAngle = 0f;

        /// <summary>
        /// The angle of the keyline component in degrees.
        /// </summary>
        private float keylineAngle = 45f;

        /// <summary>
        /// The distance between component points.
        /// </summary>
        private int distance = 4;

        /// <summary>
        /// Initializes a new instance of the <see cref="HalftoneFilter"/> class.
        /// </summary>
        public HalftoneFilter()
        {
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="HalftoneFilter"/> class.
        /// </summary>
        /// <param name="distance">
        /// The distance.
        /// </param>
        public HalftoneFilter(int distance)
        {
            this.distance = distance;
        }

        /// <summary>
        /// Gets or sets the angle of the cyan component in degrees.
        /// </summary>
        public float CyanAngle
        {
            get
            {
                return this.cyanAngle;
            }

            set
            {
                this.cyanAngle = value;
            }
        }

        /// <summary>
        /// Gets or sets the angle of the magenta component in degrees.
        /// </summary>
        public float MagentaAngle
        {
            get
            {
                return this.magentaAngle;
            }

            set
            {
                this.magentaAngle = value;
            }
        }

        /// <summary>
        /// Gets or sets the angle of the yellow component in degrees.
        /// </summary>
        public float YellowAngle
        {
            get
            {
                return this.yellowAngle;
            }

            set
            {
                this.yellowAngle = value;
            }
        }

        /// <summary>
        /// Gets or sets the angle of the keyline black component in degrees.
        /// </summary>
        public float KeylineAngle
        {
            get
            {
                return this.keylineAngle;
            }

            set
            {
                this.keylineAngle = value;
            }
        }

        /// <summary>
        /// Gets or sets the distance between component points.
        /// </summary>
        public int Distance
        {
            get
            {
                return this.distance;
            }

            set
            {
                this.distance = value;
            }
        }

        /// <summary>
        /// Applies the halftone filter. 
        /// </summary>
        /// <param name="source">
        /// The <see cref="Bitmap"/> to apply the filter to.
        /// </param>
        /// <returns>
        /// The <see cref="Bitmap"/> with the filter applied.
        /// </returns>
        public Bitmap ApplyFilter(Bitmap source)
        {
            // TODO: Make this class implement an interface?
            Bitmap padded = null;
            Bitmap cyan = null;
            Bitmap magenta = null;
            Bitmap yellow = null;
            Bitmap keyline = null;
            Bitmap newImage = null;

            try
            {
                int sourceWidth = source.Width;
                int sourceHeight = source.Height;
                int width = source.Width + this.distance;
                int height = source.Height + this.distance;

                // Draw a slightly larger image, flipping the top/left pixels to prevent
                // jagged edge of output.
                padded = new Bitmap(width, height);
                padded.SetResolution(source.HorizontalResolution, source.VerticalResolution);
                using (Graphics graphicsPadded = Graphics.FromImage(padded))
                {
                    graphicsPadded.Clear(Color.White);
                    Rectangle destinationRectangle = new Rectangle(0, 0, sourceWidth + this.distance, source.Height + this.distance);
                    using (TextureBrush tb = new TextureBrush(source))
                    {
                        tb.WrapMode = WrapMode.TileFlipXY;
                        tb.TranslateTransform(this.distance, this.distance);
                        graphicsPadded.FillRectangle(tb, destinationRectangle);
                    }
                }

                // Calculate min and max widths/heights.
                Rectangle rotatedBounds = this.GetBoundingRectangle(width, height);
                int minY = -(rotatedBounds.Height + height);
                int maxY = rotatedBounds.Height + height;
                int minX = -(rotatedBounds.Width + width);
                int maxX = rotatedBounds.Width + width;
                Point center = Point.Empty;

                // Yellow oversaturates the output.
                int offset = this.distance;
                float yellowMultiplier = this.distance * 1.334f;
                float magentaMultiplier = this.distance * 1.667f;
                float multiplier = this.distance * 2.2f;
                float max = this.distance * (float)Math.Sqrt(2);

                // Bump up the keyline max so that black looks black.
                float keylineMax = max * (float)Math.Sqrt(2);

                // Color sampled process colours from Wikipedia pages. 
                // Keyline brush is declared separately.
                Brush cyanBrush = new SolidBrush(Color.FromArgb(0, 183, 235));
                Brush magentaBrush = new SolidBrush(Color.FromArgb(255, 0, 144));
                Brush yellowBrush = new SolidBrush(Color.FromArgb(255, 239, 0));

                // Create our images.
                cyan = new Bitmap(width, height);
                magenta = new Bitmap(width, height);
                yellow = new Bitmap(width, height);
                keyline = new Bitmap(width, height);
                newImage = new Bitmap(sourceWidth, sourceHeight);

                // Ensure the correct resolution is set.
                cyan.SetResolution(source.HorizontalResolution, source.VerticalResolution);
                magenta.SetResolution(source.HorizontalResolution, source.VerticalResolution);
                yellow.SetResolution(source.HorizontalResolution, source.VerticalResolution);
                keyline.SetResolution(source.HorizontalResolution, source.VerticalResolution);
                newImage.SetResolution(source.HorizontalResolution, source.VerticalResolution);

                // Check bounds against this.
                Rectangle rectangle = new Rectangle(0, 0, width, height);

                using (Graphics graphicsCyan = Graphics.FromImage(cyan))
                using (Graphics graphicsMagenta = Graphics.FromImage(magenta))
                using (Graphics graphicsYellow = Graphics.FromImage(yellow))
                using (Graphics graphicsKeyline = Graphics.FromImage(keyline))
                {
                    // Set the quality properties.
                    graphicsCyan.PixelOffsetMode = PixelOffsetMode.HighQuality;
                    graphicsMagenta.PixelOffsetMode = PixelOffsetMode.HighQuality;
                    graphicsYellow.PixelOffsetMode = PixelOffsetMode.HighQuality;
                    graphicsKeyline.PixelOffsetMode = PixelOffsetMode.HighQuality;

                    graphicsCyan.SmoothingMode = SmoothingMode.AntiAlias;
                    graphicsMagenta.SmoothingMode = SmoothingMode.AntiAlias;
                    graphicsYellow.SmoothingMode = SmoothingMode.AntiAlias;
                    graphicsKeyline.SmoothingMode = SmoothingMode.AntiAlias;

                    graphicsCyan.CompositingQuality = CompositingQuality.HighQuality;
                    graphicsMagenta.CompositingQuality = CompositingQuality.HighQuality;
                    graphicsYellow.CompositingQuality = CompositingQuality.HighQuality;
                    graphicsKeyline.CompositingQuality = CompositingQuality.HighQuality;

                    // Set up the canvas.
                    graphicsCyan.Clear(Color.White);
                    graphicsMagenta.Clear(Color.White);
                    graphicsYellow.Clear(Color.White);
                    graphicsKeyline.Clear(Color.White);

                    // This is too slow. The graphics object can't be called within a parallel 
                    // loop so we have to do it old school. :(
                    using (FastBitmap sourceBitmap = new FastBitmap(padded))
                    {
                        for (int y = minY; y < maxY; y += offset)
                        {
                            for (int x = minX; x < maxX; x += offset)
                            {
                                Color color;
                                CmykColor cmykColor;
                                float brushWidth;

                                // Cyan
                                Point rotatedPoint = ImageMaths.RotatePoint(new Point(x, y), this.cyanAngle, center);
                                int angledX = rotatedPoint.X;
                                int angledY = rotatedPoint.Y;
                                if (rectangle.Contains(new Point(angledX, angledY)))
                                {
                                    color = sourceBitmap.GetPixel(angledX, angledY);
                                    cmykColor = color;
                                    brushWidth = Math.Min((cmykColor.C / 100f) * multiplier, max);
                                    graphicsCyan.FillEllipse(cyanBrush, angledX, angledY, brushWidth, brushWidth);
                                }

                                // Magenta
                                rotatedPoint = ImageMaths.RotatePoint(new Point(x, y), this.magentaAngle, center);
                                angledX = rotatedPoint.X;
                                angledY = rotatedPoint.Y;
                                if (rectangle.Contains(new Point(angledX, angledY)))
                                {
                                    color = sourceBitmap.GetPixel(angledX, angledY);
                                    cmykColor = color;
                                    brushWidth = Math.Min((cmykColor.M / 100f) * magentaMultiplier, max);
                                    graphicsMagenta.FillEllipse(magentaBrush, angledX, angledY, brushWidth, brushWidth);
                                }

                                // Yellow
                                rotatedPoint = ImageMaths.RotatePoint(new Point(x, y), this.yellowAngle, center);
                                angledX = rotatedPoint.X;
                                angledY = rotatedPoint.Y;
                                if (rectangle.Contains(new Point(angledX, angledY)))
                                {
                                    color = sourceBitmap.GetPixel(angledX, angledY);
                                    cmykColor = color;
                                    brushWidth = Math.Min((cmykColor.Y / 100f) * yellowMultiplier, max);
                                    graphicsYellow.FillEllipse(yellowBrush, angledX, angledY, brushWidth, brushWidth);
                                }

                                // Keyline 
                                rotatedPoint = ImageMaths.RotatePoint(new Point(x, y), this.keylineAngle, center);
                                angledX = rotatedPoint.X;
                                angledY = rotatedPoint.Y;
                                if (rectangle.Contains(new Point(angledX, angledY)))
                                {
                                    color = sourceBitmap.GetPixel(angledX, angledY);
                                    cmykColor = color;
                                    brushWidth = Math.Min((cmykColor.K / 100f) * multiplier, keylineMax);

                                    // Just using black is too dark. 
                                    Brush keylineBrush = new SolidBrush(CmykColor.FromCmykColor(0, 0, 0, cmykColor.K));
                                    graphicsKeyline.FillEllipse(keylineBrush, angledX, angledY, brushWidth, brushWidth);
                                }
                            }
                        }
                    }

                    // Set our white background.
                    using (Graphics graphics = Graphics.FromImage(newImage))
                    {
                        graphics.Clear(Color.White);
                    }

                    // Blend the colors now to mimic adaptive blending.
                    using (FastBitmap cyanBitmap = new FastBitmap(cyan))
                    using (FastBitmap magentaBitmap = new FastBitmap(magenta))
                    using (FastBitmap yellowBitmap = new FastBitmap(yellow))
                    using (FastBitmap keylineBitmap = new FastBitmap(keyline))
                    using (FastBitmap destinationBitmap = new FastBitmap(newImage))
                    {
                        Parallel.For(
                            offset,
                            height,
                            y =>
                            {
                                for (int x = offset; x < width; x++)
                                {
                                    // ReSharper disable AccessToDisposedClosure
                                    Color cyanPixel = cyanBitmap.GetPixel(x, y);
                                    Color magentaPixel = magentaBitmap.GetPixel(x, y);
                                    Color yellowPixel = yellowBitmap.GetPixel(x, y);
                                    Color keylinePixel = keylineBitmap.GetPixel(x, y);

                                    // Negate the offset.
                                    int xBack = x - offset;
                                    int yBack = y - offset;

                                    CmykColor blended = cyanPixel.AddAsCmykColor(magentaPixel, yellowPixel, keylinePixel);
                                    if (rectangle.Contains(new Point(xBack, yBack)))
                                    {
                                        destinationBitmap.SetPixel(xBack, yBack, blended);
                                    }
                                    // ReSharper restore AccessToDisposedClosure
                                }
                            });
                    }
                }

                padded.Dispose();
                cyan.Dispose();
                magenta.Dispose();
                yellow.Dispose();
                keyline.Dispose();
                source.Dispose();
                source = newImage;
            }
            catch
            {
                if (padded != null)
                {
                    padded.Dispose();
                }

                if (cyan != null)
                {
                    cyan.Dispose();
                }

                if (magenta != null)
                {
                    magenta.Dispose();
                }

                if (yellow != null)
                {
                    yellow.Dispose();
                }

                if (keyline != null)
                {
                    keyline.Dispose();
                }

                if (newImage != null)
                {
                    newImage.Dispose();
                }
            }

            return source;
        }

        /// <summary>
        /// Gets the bounding rectangle of the image based on the rotating angles.
        /// </summary>
        /// <param name="width">
        /// The width of the image.
        /// </param>
        /// <param name="height">
        /// The height of the image.
        /// </param>
        /// <returns>
        /// The <see cref="Rectangle"/>.
        /// </returns>
        private Rectangle GetBoundingRectangle(int width, int height)
        {
            int maxWidth = 0;
            int maxHeight = 0;
            List<float> angles = new List<float> { this.CyanAngle, this.MagentaAngle, this.YellowAngle, this.KeylineAngle };

            foreach (float angle in angles)
            {
                Size rotatedSize = ImageMaths.GetBoundingRotatedRectangle(width, height, angle).Size;
                maxWidth = Math.Max(maxWidth, rotatedSize.Width);
                maxHeight = Math.Max(maxHeight, rotatedSize.Height);
            }

            return new Rectangle(0, 0, maxWidth, maxHeight);
        }
    }
}
