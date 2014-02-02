// --------------------------------------------------------------------------------------------------------------------
// <copyright file="ImageUtils.cs" company="James South">
//   Copyright (c) James South.
//   Licensed under the Apache License, Version 2.0.
// </copyright>
// <summary>
//   Encapsulates useful image utility methods.
// </summary>
// --------------------------------------------------------------------------------------------------------------------

namespace ImageProcessor.Imaging
{
    #region Using
    using System.Drawing;
    using System.Drawing.Imaging;
    #endregion

    /// <summary>
    /// Encapsulates useful image utility methods.
    /// </summary>
    public static class ImageUtils
    {
        /// <summary>
        /// Returns an instance of EncodingParameters for jpeg compression. 
        /// </summary>
        /// <param name="quality">The quality to return the image at.</param>
        /// <returns>The encodingParameters for jpeg compression. </returns>
        public static EncoderParameters GetEncodingParameters(int quality)
        {
            EncoderParameters encoderParameters = null;
            try
            {
                // Create a series of encoder parameters.
                encoderParameters = new EncoderParameters(1);

                // Set the quality.
                encoderParameters.Param[0] = new EncoderParameter(Encoder.Quality, quality);
            }
            catch
            {
                if (encoderParameters != null)
                {
                    encoderParameters.Dispose();
                }
            }

            return encoderParameters;
        }

        /// <summary>Returns a value indicating whether or not the given bitmap is indexed.</summary>
        /// <param name="image">The image to check</param>
        /// <returns>Whether or not the given bitmap is indexed.</returns>
        public static bool IsIndexed(Image image)
        {
            // Test value of flags using bitwise AND.
            // ReSharper disable once BitwiseOperatorOnEnumWithoutFlags
            return (image.PixelFormat & PixelFormat.Indexed) != 0;
        }
    }
}
