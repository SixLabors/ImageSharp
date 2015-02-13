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

                int minHeight = -height * 2;
                int maxHeight = height * 2;
                int minWidth = -width * 2;
                int maxWidth = width * 2;

                float multiplier = 4 * (float)Math.Sqrt(2);
                float max = this.distance + ((float)Math.Sqrt(2) / 2);
                float keylineMax = this.distance + (float)Math.Sqrt(2) + ((float)Math.Sqrt(2) / 2);

                // Color sampled process colours from Wikipedia pages. 
                // Keyline brush is declared separately.
                Brush cyanBrush = new SolidBrush(Color.FromArgb(0, 183, 235));
                Brush magentaBrush = new SolidBrush(Color.FromArgb(0, 183, 235));
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
                        for (int y = minHeight; y < maxHeight; y += d)
                        {
                            for (int x = minWidth; x < maxWidth; x += d)
                            {
                                Color color;
                                CmykColor cmykColor;
                                float brushWidth;

                                // Cyan
                                Point rotatedPoint = ImageMaths.RotatePoint(new Point(x, y), this.cyanAngle);
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
                                rotatedPoint = ImageMaths.RotatePoint(new Point(x, y), this.magentaAngle);
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
                                rotatedPoint = ImageMaths.RotatePoint(new Point(x, y), this.yellowAngle);
                                angledX = rotatedPoint.X;
                                angledY = rotatedPoint.Y;
                                if (rectangle.Contains(new Point(angledX, angledY)))
                                {
                                    color = sourceBitmap.GetPixel(angledX, angledY);
                                    cmykColor = color;
                                    brushWidth = ImageMaths.Clamp(d * (cmykColor.Y / 255f) * multiplier, 0, max);
                                    graphicsYellow.FillEllipse(yellowBrush, angledX, angledY, brushWidth, brushWidth);
                                }

                                // Keyline 
                                rotatedPoint = ImageMaths.RotatePoint(new Point(x, y), this.keylineAngle);
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

        private void SetGraphicsSettings(ref Graphics graphics)
        {
            // Set the quality properties.
            graphics.SmoothingMode = graphics.SmoothingMode = SmoothingMode.AntiAlias;
            graphics.InterpolationMode = InterpolationMode.HighQualityBicubic;
            graphics.PixelOffsetMode = PixelOffsetMode.HighQuality;
            graphics.CompositingQuality = CompositingQuality.HighQuality;

            // Set up the canvas.
            graphics.Clear(Color.Transparent);
        }
    }
}
