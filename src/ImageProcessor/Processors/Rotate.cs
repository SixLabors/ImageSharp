// -----------------------------------------------------------------------
// <copyright file="Rotate.cs" company="James South">
// TODO: Update copyright text.
// </copyright>
// -----------------------------------------------------------------------

namespace ImageProcessor.Processors
{
    #region Using
    using System;
    using System.Collections.Generic;
    using System.Drawing;
    using System.Drawing.Drawing2D;
    using System.Text.RegularExpressions;
    using ImageProcessor.Imaging;
    #endregion

    /// <summary>
    /// Encapsulates methods to rotate an image.
    /// </summary>
    public class Rotate : IGraphicsProcessor
    {
        /// <summary>
        /// The regular expression to search strings for.
        /// </summary>
        private static readonly Regex QueryRegex = new Regex(@"rotate=([1-2][0-9][0-9]|3[0-5][0-9]|\d{1}(?!\d)|\d{1,2}(?!\d)|360)|rotate=[^&]*", RegexOptions.Compiled);

        /// <summary>
        /// The regular expression to search strings for the angle attribute.
        /// </summary>
        private static readonly Regex AngleRegex = new Regex(@"rotate=angle-([1-2][0-9][0-9]|3[0-5][0-9]|\d{1}(?!\d)|\d{1,2}(?!\d)|360)", RegexOptions.Compiled);

        /// <summary>
        /// The regular expression to search strings for the color attribute.
        /// </summary>
        private static readonly Regex ColorRegex = new Regex(@"bgcolor-([0-9a-fA-F]{3}){1,2}", RegexOptions.Compiled);

        #region IGraphicsProcessor Members
        /// <summary>
        /// Gets the name.
        /// </summary>
        public string Name
        {
            get
            {
                return "Rotate";
            }
        }

        /// <summary>
        /// Gets the description.
        /// </summary>
        public string Description
        {
            get
            {
                return "Rotates an image at the given angle.";
            }
        }

        /// <summary>
        /// Gets the regular expression to search strings for.
        /// </summary>
        public Regex RegexPattern
        {
            get
            {
                return QueryRegex;
            }
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
        /// Gets the order in which this processor is to be used in a chain.
        /// </summary>
        public int SortOrder
        {
            get;
            private set;
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
        /// The position in the original string where the first character of the captured substring was found.
        /// </summary>
        /// <param name="queryString">
        /// The query string to search.
        /// </param>
        /// <returns>
        /// The zero-based starting position in the original string where the captured substring was found.
        /// </returns>
        public int MatchRegexIndex(string queryString)
        {
            int index = 0;

            // Set the sort order to max to allow filtering.
            this.SortOrder = int.MaxValue;

            foreach (Match match in this.RegexPattern.Matches(queryString))
            {
                if (match.Success)
                {
                    if (index == 0)
                    {
                        // Set the index on the first instance only.
                        this.SortOrder = match.Index;

                        RotateLayer rotateLayer = new RotateLayer();

                        string toParse = match.Value;

                        if (toParse.Contains("bgcolor"))
                        {
                            rotateLayer.Angle = this.ParseAngle(toParse);
                            rotateLayer.BackgroundColor = this.ParseColor(toParse);
                        }
                        else
                        {
                            int degrees;
                            int.TryParse(match.Value.Split('=')[1], out degrees);

                            rotateLayer.Angle = degrees;
                        }

                        this.DynamicParameter = rotateLayer;
                    }

                    index += 1;
                }
            }

            return this.SortOrder;
        }

        /// <summary>
        /// Processes the image.
        /// </summary>
        /// <param name="factory">
        /// The the current instance of the <see cref="T:ImageProcessor.ImageFactory"/> class containing
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
                RotateLayer rotateLayer = this.DynamicParameter;
                int angle = rotateLayer.Angle;
                Color backgroundColor = rotateLayer.BackgroundColor;

                // Center of the image
                float rotateAtX = Math.Abs(image.Width / 2);
                float rotateAtY = Math.Abs(image.Height / 2);

                // Create a rotated image.
                newImage = this.RotateImage(image, rotateAtX, rotateAtY, angle, backgroundColor);
                newImage.Tag = image.Tag;

                image.Dispose();
                image = newImage;
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
        #endregion

        #region Private Methods
        /// <summary>
        /// Rotates an image to the given angle at the given position.
        /// </summary>
        /// <param name="image">The image to rotate</param>
        /// <param name="rotateAtX">The horizontal pixel coordinate at which to rotate the image.</param>
        /// <param name="rotateAtY">The vertical pixel coordinate at which to rotate the image.</param>
        /// <param name="angle">The angle in degress at which to rotate the image.</param>
        /// <param name="backgroundColor">The background color to fill an image with.</param>
        /// <returns>The image rotated to the given angle at the given position.</returns>
        /// <remarks> 
        /// Based on http://www.codeproject.com/Articles/58815/C-Image-PictureBox-Rotations?msg=4155374#xx4155374xx
        /// </remarks>
        private Bitmap RotateImage(Image image, float rotateAtX, float rotateAtY, float angle, Color backgroundColor)
        {
            int width, height, x, y;

            // Degrees to radians according to Google. 
            const double DegreeToRadian = 0.0174532925;

            double widthAsDouble = image.Width;
            double heightAsDouble = image.Height;

            // Allow for angles over 180
            if (angle > 180)
            {
                angle = angle - 360;
            }

            double degrees = Math.Abs(angle);

            if (degrees <= 90)
            {
                double radians = DegreeToRadian * degrees;
                double radiansSin = Math.Sin(radians);
                double radiansCos = Math.Cos(radians);
                width = (int)((heightAsDouble * radiansSin) + (widthAsDouble * radiansCos));
                height = (int)((widthAsDouble * radiansSin) + (heightAsDouble * radiansCos));
                x = (width - image.Width) / 2;
                y = (height - image.Height) / 2;
            }
            else
            {
                degrees -= 90;
                double radians = DegreeToRadian * degrees;
                double radiansSin = Math.Sin(radians);
                double radiansCos = Math.Cos(radians);

                // Fix the 270 error
                if (Math.Abs(radiansCos - -1.0D) < 0.00001)
                {
                    radiansCos = 1;
                }

                width = (int)((widthAsDouble * radiansSin) + (heightAsDouble * radiansCos));
                height = (int)((heightAsDouble * radiansSin) + (widthAsDouble * radiansCos));
                x = (width - image.Width) / 2;
                y = (height - image.Height) / 2;
            }

            // Create a new empty bitmap to hold rotated image
            Bitmap newImage = new Bitmap(width, height);
            newImage.SetResolution(image.HorizontalResolution, image.VerticalResolution);

            // Make a graphics object from the empty bitmap
            using (Graphics graphics = Graphics.FromImage(newImage))
            {
                // Reduce the jagged edge.
                graphics.SmoothingMode = SmoothingMode.HighQuality;

                // Contrary to everything I have read bicubic is producing the best results.
                graphics.InterpolationMode = InterpolationMode.HighQualityBicubic;
                graphics.PixelOffsetMode = PixelOffsetMode.HighQuality;
                graphics.CompositingQuality = CompositingQuality.HighSpeed;

                // Fill the background.
                graphics.Clear(backgroundColor);

                // Put the rotation point in the "center" of the image
                graphics.TranslateTransform(rotateAtX + x, rotateAtY + y);

                // Rotate the image
                graphics.RotateTransform(angle);

                // Move the image back
                graphics.TranslateTransform(-rotateAtX - x, -rotateAtY - y);

                // Draw passed in image onto graphics object
                graphics.DrawImage(image, new PointF(0 + x, 0 + y));
            }

            return newImage;
        }

        /// <summary>
        /// Returns the correct <see cref="T:System.Int32"/> containing the angle for the given string.
        /// </summary>
        /// <param name="input">
        /// The input string containing the value to parse.
        /// </param>
        /// <returns>
        /// The correct <see cref="T:System.Int32"/> containing the angle for the given string.
        /// </returns>
        private int ParseAngle(string input)
        {
            foreach (Match match in AngleRegex.Matches(input))
            {
                // Split on angle-
                int angle;
                int.TryParse(match.Value.Split('-')[1], out angle);
                return angle;
            }

            // No rotate - matches the RotateLayer default.
            return 0;
        }

        /// <summary>
        /// Returns the correct <see cref="T:System.Drawing.Color"/> for the given string.
        /// </summary>
        /// <param name="input">
        /// The input string containing the value to parse.
        /// </param>
        /// <returns>
        /// The correct <see cref="T:System.Drawing.Color"/>
        /// </returns>
        private Color ParseColor(string input)
        {
            foreach (Match match in ColorRegex.Matches(input))
            {
                // split on color-hex
                return ColorTranslator.FromHtml("#" + match.Value.Split('-')[1]);
            }

            return Color.Transparent;
        }
        #endregion
    }
}
