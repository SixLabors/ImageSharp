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
        /// Applies the filter. TODO: Make this class implement an interface?
        /// </summary>
        /// <param name="source">
        /// The <see cref="Bitmap"/> to apply the filter to.
        /// </param>
        /// <returns>
        /// The <see cref="Bitmap"/> with the filter applied.
        /// </returns>
        public Bitmap ApplyFilter(Bitmap source)
        {
            Bitmap cyan = null;
            Bitmap magenta = null;
            Bitmap yellow = null;
            Bitmap keyline = null;
            Bitmap newImage = null;

            try
            {
                int width = source.Width;
                int height = source.Height;

                // Calculate min and max widths/heights.
                Rectangle rotatedBounds = this.GetBoundingRectangle(width, height);
                int minY = -(rotatedBounds.Height + height);
                int maxY = rotatedBounds.Height + height;
                int minX = -(rotatedBounds.Width + width);
                int maxX = rotatedBounds.Width + width;
                Point center = Point.Empty;

                // Yellow oversaturates the output.
                const float YellowMultiplier = 4;
                float multiplier = YellowMultiplier * (float)Math.Sqrt(2);

                float max = this.distance;

                // Bump up the keyline max so that black looks black.
                float keylineMax = max + ((float)Math.Sqrt(2) * 1.5f);

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
                newImage = new Bitmap(width, height);

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

                    // Set up the canvas.
                    graphicsCyan.Clear(Color.Transparent);
                    graphicsMagenta.Clear(Color.Transparent);
                    graphicsYellow.Clear(Color.Transparent);
                    graphicsKeyline.Clear(Color.Transparent);

                    int d = this.distance;

                    // This is too slow. The graphics object can't be called within a parallel 
                    // loop so we have to do it old school. :(
                    using (FastBitmap sourceBitmap = new FastBitmap(source))
                    {
                        for (int y = minY; y < maxY; y += d)
                        {
                            for (int x = minX; x < maxX; x += d)
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
                                    brushWidth = ImageMaths.Clamp(d * (cmykColor.C / 255f) * multiplier, 0, max);
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
                                    brushWidth = ImageMaths.Clamp(d * (cmykColor.M / 255f) * multiplier, 0, max);
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
                                    brushWidth = ImageMaths.Clamp(d * (cmykColor.Y / 255f) * YellowMultiplier, 0, max);
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
                                    brushWidth = ImageMaths.Clamp(d * (cmykColor.K / 255f) * multiplier, 0, keylineMax);

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
                            0,
                            height,
                            y =>
                            {
                                for (int x = 0; x < width; x++)
                                {
                                    // ReSharper disable AccessToDisposedClosure
                                    Color cyanPixel = cyanBitmap.GetPixel(x, y);
                                    Color magentaPixel = magentaBitmap.GetPixel(x, y);
                                    Color yellowPixel = yellowBitmap.GetPixel(x, y);
                                    Color keylinePixel = keylineBitmap.GetPixel(x, y);

                                    CmykColor blended = cyanPixel.AddAsCmykColor(magentaPixel, yellowPixel, keylinePixel);
                                    destinationBitmap.SetPixel(x, y, blended);
                                    // ReSharper restore AccessToDisposedClosure
                                }
                            });
                    }
                }

                cyan.Dispose();
                magenta.Dispose();
                yellow.Dispose();
                keyline.Dispose();
                source.Dispose();
                source = newImage;
            }
            catch
            {
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
                double radians = ImageMaths.DegreesToRadians(angle);
                double radiansSin = Math.Sin(radians);
                double radiansCos = Math.Cos(radians);
                double width1 = (height * radiansSin) + (width * radiansCos);
                double height1 = (width * radiansSin) + (height * radiansCos);

                // Find dimensions in the other direction
                radiansSin = Math.Sin(-radians);
                radiansCos = Math.Cos(-radians);
                double width2 = (height * radiansSin) + (width * radiansCos);
                double height2 = (width * radiansSin) + (height * radiansCos);

                int maxW = Math.Max(maxWidth, Convert.ToInt32(Math.Max(Math.Abs(width1), Math.Abs(width2))));
                int maxH = Math.Max(maxHeight, Convert.ToInt32(Math.Max(Math.Abs(height1), Math.Abs(height2))));

                maxHeight = maxH;
                maxWidth = maxW;
            }

            return new Rectangle(0, 0, maxWidth, maxHeight);
        }
    }
}
