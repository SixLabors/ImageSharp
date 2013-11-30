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
    using System;
    using System.Drawing;
    using System.Drawing.Imaging;
    using System.IO;
    using System.Linq;
    using System.Text.RegularExpressions;
    #endregion

    /// <summary>
    /// Encapsulates useful image utility methods.
    /// </summary>
    public static class ImageUtils
    {
        /// <summary>
        /// The image format regex.
        /// </summary>
        private static readonly Regex FormatRegex = new Regex(@"(\.?)(j(pg|peg)|bmp|png|gif|ti(f|ff))", RegexOptions.Compiled | RegexOptions.IgnoreCase | RegexOptions.RightToLeft);

        /// <summary>
        /// Returns the correct response type based on the given request path.
        /// </summary>
        /// <param name="request">
        /// The request to match.
        /// </param>
        /// <returns>
        /// The correct <see cref="ResponseType"/>.
        /// </returns>
        public static ResponseType GetResponseType(string request)
        {
            Match match = FormatRegex.Matches(request)[0];

            switch (match.Value.ToUpperInvariant())
            {
                case "PNG":
                case ".PNG":
                    return ResponseType.Png;
                case "BMP":
                case ".BMP":
                    return ResponseType.Bmp;
                case "GIF":
                case ".GIF":
                    return ResponseType.Gif;
                case "TIF":
                case "TIFF":
                case ".TIF":
                case ".TIFF":
                    return ResponseType.Tiff;
                default:
                    return ResponseType.Jpeg;
            }
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
                    case ".TIF":
                    case ".TIFF":
                        return ImageFormat.Tiff;
                    default:
                        // Should be a jpeg.
                        return ImageFormat.Jpeg;
                }
            }

            // TODO: Show custom exception?
            return null;
        }

        /// <summary>
        /// Returns the correct file extension for the given <see cref="T:System.Drawing.Imaging.ImageFormat"/>.
        /// </summary>
        /// <param name="imageFormat">
        /// The <see cref="T:System.Drawing.Imaging.ImageFormat"/> to return the extension for.
        /// </param>
        /// <param name="originalExtension">
        /// The original Extension.
        /// </param>
        /// <returns>
        /// The correct file extension for the given <see cref="T:System.Drawing.Imaging.ImageFormat"/>.
        /// </returns>
        public static string GetExtensionFromImageFormat(ImageFormat imageFormat, string originalExtension)
        {
            switch (imageFormat.ToString())
            {
                case "Gif":
                    return ".gif";
                case "Bmp":
                    return ".bmp";
                case "Png":
                    return ".png";
                case "Tif":
                case "Tiff":
                    if (!string.IsNullOrWhiteSpace(originalExtension) && originalExtension.ToUpperInvariant() == ".TIFF")
                    {
                        return ".tiff";
                    }

                    return ".tif";
                default:
                    if (!string.IsNullOrWhiteSpace(originalExtension) && originalExtension.ToUpperInvariant() == ".JPEG")
                    {
                        return ".jpeg";
                    }

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
                case ResponseType.Tiff:
                    return ImageFormat.Tiff;
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

        /// <summary>
        /// Checks a given string to check whether the value contains a valid image extension.
        /// </summary>
        /// <param name="fileName">The string containing the filename to check.</param>
        /// <returns>True the value contains a valid image extension, otherwise false.</returns>
        public static bool IsValidImageExtension(string fileName)
        {
            return FormatRegex.IsMatch(fileName);
        }

        /// <summary>
        /// Returns the correct file extension for the given string input
        /// </summary>
        /// <param name="input">
        /// The string to parse.
        /// </param>
        /// <returns>
        /// The correct file extension for the given string input if it can find one; otherwise an empty string.
        /// </returns>
        public static string GetExtension(string input)
        {
            Match match = FormatRegex.Matches(input)[0];

            return match.Success ? match.Value : string.Empty;
        }

        /// <summary>Returns a value indicating whether or not the given bitmap is indexed.</summary>
        /// <param name="image">The image to check</param>
        /// <returns>Whether or not the given bitmap is indexed.</returns>
        public static bool IsIndexed(Image image)
        {
            // Test value of flags using bitwise AND.
            return (image.PixelFormat & PixelFormat.Indexed) != 0;
        }
    }
}
