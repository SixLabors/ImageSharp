// --------------------------------------------------------------------------------------------------------------------
// <copyright file="ImageFormatExtensions.cs" company="James South">
//   Copyright (c) James South.
//   Licensed under the Apache License, Version 2.0.
// </copyright>
// <summary>
//   Encapsulates a series of time saving extension methods to the <see cref="T:System.Drawing.Imaging.ImageFormat" />
//   class.
// </summary>
// --------------------------------------------------------------------------------------------------------------------

namespace ImageProcessor.Core.Common.Extensions
{
    #region

    using System.Drawing.Imaging;
    using System.Linq;

    #endregion

    /// <summary>
    /// Encapsulates a series of time saving extension methods to the <see cref="T:System.Drawing.Imaging.ImageFormat" /> class.
    /// </summary>
    public static class ImageFormatExtensions
    {
        /// <summary>
        /// Gets the correct mime-type for the given <see cref="T:System.Drawing.Imaging.ImageFormat" />.
        /// </summary>
        /// <param name="imageFormat">The <see cref="T:System.Drawing.Imaging.ImageFormat" />.</param>
        /// <returns>The correct mime-type for the given <see cref="T:System.Drawing.Imaging.ImageFormat" />.</returns>
        public static string GetMimeType(this ImageFormat imageFormat)
        {
            if (imageFormat.Equals(ImageFormat.Icon))
            {
                return "image/x-icon";
            }

            ImageCodecInfo[] codecs = ImageCodecInfo.GetImageEncoders();
            return codecs.First(codec => codec.FormatID == imageFormat.Guid).MimeType;
        }

        /// <summary>
        /// Gets the name for the given <see cref="T:System.Drawing.Imaging.ImageFormat" />.
        /// </summary>
        /// <param name="format">
        /// The <see cref="T:System.Drawing.Imaging.ImageFormat" /> to get the name for.
        /// </param>
        /// <returns>
        /// The <see cref="string"/> representing the name of the <see cref="T:System.Drawing.Imaging.ImageFormat" />.
        /// </returns>
        public static string GetName(this ImageFormat format)
        {
            if (format.Guid == ImageFormat.MemoryBmp.Guid)
            {
                return "MemoryBMP";
            }

            if (format.Guid == ImageFormat.Bmp.Guid)
            {
                return "Bmp";
            }

            if (format.Guid == ImageFormat.Emf.Guid)
            {
                return "Emf";
            }

            if (format.Guid == ImageFormat.Wmf.Guid)
            {
                return "Wmf";
            }

            if (format.Guid == ImageFormat.Gif.Guid)
            {
                return "Gif";
            }

            if (format.Guid == ImageFormat.Jpeg.Guid)
            {
                return "Jpeg";
            }

            if (format.Guid == ImageFormat.Png.Guid)
            {
                return "Png";
            }

            if (format.Guid == ImageFormat.Tiff.Guid)
            {
                return "Tiff";
            }

            if (format.Guid == ImageFormat.Exif.Guid)
            {
                return "Exif";
            }

            if (format.Guid == ImageFormat.Icon.Guid)
            {
                return "Icon";
            }

            return "[ImageFormat: " + format.Guid + "]";
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
        public static string GetFileExtension(this ImageFormat imageFormat, string originalExtension)
        {
            string name = imageFormat.GetName();

            switch (name)
            {
                case "Icon":
                    return ".ico";
                case "Gif":
                    return ".gif";
                case "Bmp":
                    return ".bmp";
                case "Png":
                    return ".png";
                case "Tiff":
                    if (!string.IsNullOrWhiteSpace(originalExtension) && originalExtension.ToUpperInvariant() == ".TIF")
                    {
                        return ".tif";
                    }

                    return ".tiff";
                case "Jpeg":
                    if (!string.IsNullOrWhiteSpace(originalExtension) && originalExtension.ToUpperInvariant() == ".JPG")
                    {
                        return ".jpg";
                    }

                    break;
            }

            return null;
        }
    }
}