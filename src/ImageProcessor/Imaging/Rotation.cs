namespace ImageProcessor.Imaging
{
    using System;
    using System.Drawing;

    /// <summary>
    /// Provides rotation calculation methods
    /// </summary>
    internal class Rotation
    {
        /// <summary>
        /// Calculates the new size after rotation.
        /// </summary>
        /// <param name="width">The width of the image.</param>
        /// <param name="height">The height of the image.</param>
        /// <param name="angle">The angle of rotation.</param>
        /// <returns>The new size of the image</returns>
        public static Size NewSizeAfterRotation(int width, int height, float angle)
        {
            double widthAsDouble = width;
            double heightAsDouble = height;

            double radians = angle * Math.PI / 180d;
            double radiansSin = Math.Sin(radians);
            double radiansCos = Math.Cos(radians);
            double width1 = (heightAsDouble * radiansSin) + (widthAsDouble * radiansCos);
            double height1 = (widthAsDouble * radiansSin) + (heightAsDouble * radiansCos);

            // Find dimensions in the other direction
            radiansSin = Math.Sin(-radians);
            radiansCos = Math.Cos(-radians);
            double width2 = (heightAsDouble * radiansSin) + (widthAsDouble * radiansCos);
            double height2 = (widthAsDouble * radiansSin) + (heightAsDouble * radiansCos);

            // Get the external vertex for the rotation
            Size result = new Size();
            result.Width = Convert.ToInt32(Math.Max(Math.Abs(width1), Math.Abs(width2)));
            result.Height = Convert.ToInt32(Math.Max(Math.Abs(height1), Math.Abs(height2)));

            return result;
        }

        /// <summary>
        /// Calculates the zoom needed after the rotation.
        /// </summary>
        /// <param name="imageWidth">Width of the image.</param>
        /// <param name="imageHeight">Height of the image.</param>
        /// <param name="angle">The angle.</param>
        /// <remarks>
        /// Based on <see href="http://math.stackexchange.com/questions/1070853/"/>
        /// </remarks>
        /// <returns>The zoom needed</returns>
        public static float ZoomAfterRotation(int imageWidth, int imageHeight, float angle)
        {
            double radians = angle * Math.PI / 180d;
            double radiansSin = Math.Sin(radians);
            double radiansCos = Math.Cos(radians);

            double widthRotated = (imageWidth * radiansCos) + (imageHeight * radiansSin);
            double heightRotated = (imageWidth * radiansSin) + (imageHeight * radiansCos);

            return (float)Math.Max(widthRotated / imageWidth, heightRotated / imageHeight);
        }
    }
}