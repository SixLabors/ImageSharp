// -----------------------------------------------------------------------
// <copyright file="ImageUtils.cs" company="James South">
//     Copyright (c) James South.
//     Licensed under the Apache License, Version 2.0.
// </copyright>
// -----------------------------------------------------------------------

namespace ImageProcessor.Imaging
{
    #region Using
    using System;
    using System.Drawing.Imaging;
    using System.IO;
    using System.Linq;
    using System.Threading.Tasks;
    #endregion

    /// <summary>
    /// Encapsulates useful image utility methods.
    /// </summary>
    public static class ImageUtils
    {
        /// <summary>
        /// Returns the correct response type based on the given file extension.
        /// </summary>
        /// <param name="fileName">The string containing the filename to check against.</param>
        /// <returns>The correct response type based on the given file extension.</returns>
        public static ResponseType GetResponseType(string fileName)
        {
            string extension = Path.GetExtension(fileName);
            if (extension != null)
            {
                string ext = extension.ToUpperInvariant();

                switch (ext)
                {
                    case ".PNG":
                        return ResponseType.Png;
                    case ".BMP":
                        return ResponseType.Bmp;
                    case ".GIF":
                        return ResponseType.Gif;
                    default:
                        // Should be a jpeg.
                        return ResponseType.Jpeg;
                }
            }

            // TODO: Should we call this on bad request?
            return ResponseType.Jpeg;
        }

        /// <summary>
        /// Returns the correct image format based on the given file extension.
        /// </summary>
        /// <param name="fileName">The string containing the filename to check against.</param>
        /// <returns>The correct image format based on the given filename.</returns>
        public static ImageFormat GetImageFormat(string fileName)
        {
            string extension = Path.GetExtension(fileName);
            if (extension != null)
            {
                string ext = extension.ToUpperInvariant();

                switch (ext)
                {
                    case ".PNG":
                        return ImageFormat.Png;
                    case ".BMP":
                        return ImageFormat.Bmp;
                    case ".GIF":
                        return ImageFormat.Gif;
                    default:
                        // Should be a jpeg.
                        return ImageFormat.Jpeg;
                }
            }

            // TODO: Show custom exception??
            return null;
        }

        /// <summary>
        /// Returns the correct file extension for the given <see cref="T:System.Drawing.Imaging.ImageFormat"/>.
        /// </summary>
        /// <param name="imageFormat">
        /// The <see cref="T:System.Drawing.Imaging.ImageFormat"/> to return the extension for.
        /// </param>
        /// <returns>
        /// The correct file extension for the given <see cref="T:System.Drawing.Imaging.ImageFormat"/>.
        /// </returns>
        public static string GetExtensionFromImageFormat(ImageFormat imageFormat)
        {
            switch (imageFormat.ToString())
            {
                case "Gif":
                    return ".gif";
                case "Bmp":
                    return ".bmp";
                case "Png":
                    return ".png";
                default:
                    return ".jpg";
            }
        }

        /// <summary>
        /// Returns the correct image format based on the given response type.
        /// </summary>
        /// <param name="responseType">
        /// The <see cref="ImageProcessor.Imaging.ResponseType"/> to check against.
        /// </param>
        /// <returns>The correct image format based on the given response type.</returns>
        public static ImageFormat GetImageFormat(ResponseType responseType)
        {
            switch (responseType)
            {
                case ResponseType.Png:
                    return ImageFormat.Png;
                case ResponseType.Bmp:
                    return ImageFormat.Bmp;
                case ResponseType.Gif:
                    return ImageFormat.Gif;
                default:
                    // Should be a jpeg.
                    return ImageFormat.Jpeg;
            }
        }

        /// <summary>
        /// Returns the first ImageCodeInfo instance with the specified mime type. 
        /// </summary>
        /// <param name="mimeType">
        /// A string that contains the codec's Multipurpose Internet Mail Extensions (MIME) type.
        /// </param>
        /// <returns>
        /// The first ImageCodeInfo instance with the specified mime type. 
        /// </returns>
        public static ImageCodecInfo GetImageCodeInfo(string mimeType)
        {
            ImageCodecInfo[] info = ImageCodecInfo.GetImageEncoders();
            return info.FirstOrDefault(ici => ici.MimeType.Equals(mimeType, StringComparison.OrdinalIgnoreCase));
        }

        /// <summary>
        /// Returns an instance of EncodingParameters for jpeg comression. 
        /// </summary>
        /// <param name="quality">The quality to return the image at.</param>
        /// <returns>The encodingParameters for jpeg comression. </returns>
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

        /// <summary>
        /// Checks a given string to check whether the value contains a valid image extension.
        /// </summary>
        /// <param name="fileName">The string containing the filename to check.</param>
        /// <returns>True the value contains a valid image extension, otherwise false.</returns>
        public static bool IsValidImageExtension(string fileName)
        {
            bool isValid = false;

            if (!string.IsNullOrWhiteSpace(fileName))
            {
                string[] fileExtensions = { ".BMP", ".JPG", ".PNG", ".GIF", ".JPEG" };

                Parallel.ForEach(
                    fileExtensions,
                    (extension, loop) =>
                    {
                        if (fileName.ToUpperInvariant().EndsWith(extension))
                        {
                            isValid = true;
                            loop.Stop();
                        }
                    });
            }

            return isValid;
        }
    }
}
