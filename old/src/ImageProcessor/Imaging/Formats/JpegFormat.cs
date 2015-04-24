// --------------------------------------------------------------------------------------------------------------------
// <copyright file="JpegFormat.cs" company="James South">
//   Copyright (c) James South.
//   Licensed under the Apache License, Version 2.0.
// </copyright>
// <summary>
//   Provides the necessary information to support jpeg images.
// </summary>
// --------------------------------------------------------------------------------------------------------------------

namespace ImageProcessor.Imaging.Formats
{
    using System;
    using System.Collections.Generic;
    using System.Drawing;
    using System.Drawing.Imaging;
    using System.IO;
    using System.Linq;

    /// <summary>
    /// Provides the necessary information to support jpeg images.
    /// </summary>
    public sealed class JpegFormat : FormatBase
    {
        /// <summary>
        /// Gets the file headers.
        /// </summary>
        public override byte[][] FileHeaders
        {
            get
            {
                return new[] { new byte[] { 255, 216, 255 } };
            }
        }

        /// <summary>
        /// Gets the list of file extensions.
        /// </summary>
        public override string[] FileExtensions
        {
            get
            {
                return new[] { "jpeg", "jpg" };
            }
        }

        /// <summary>
        /// Gets the standard identifier used on the Internet to indicate the type of data that a file contains. 
        /// </summary>
        public override string MimeType
        {
            get
            {
                return "image/jpeg";
            }
        }

        /// <summary>
        /// Gets the <see cref="ImageFormat" />.
        /// </summary>
        public override ImageFormat ImageFormat
        {
            get
            {
                return ImageFormat.Jpeg;
            }
        }

        /// <summary>
        /// Applies the given processor the current image.
        /// </summary>
        /// <param name="processor">The processor delegate.</param>
        /// <param name="factory">The <see cref="ImageFactory" />.</param>
        public override void ApplyProcessor(Func<ImageFactory, Image> processor, ImageFactory factory)
        {
            base.ApplyProcessor(processor, factory);

            // Set the property item information from any Exif metadata.
            // We do this here so that they can be changed between processor methods.
            if (factory.PreserveExifData)
            {
                foreach (KeyValuePair<int, PropertyItem> propertItem in factory.ExifPropertyItems)
                {
                    factory.Image.SetPropertyItem(propertItem.Value);
                }
            }
        }

        /// <summary>
        /// Saves the current image to the specified output stream.
        /// </summary>
        /// <param name="stream">
        /// The <see cref="T:System.IO.Stream" /> to save the image information to.
        /// </param>
        /// <param name="image">The <see cref="T:System.Drawing.Image" /> to save.</param>
        /// <returns>
        /// The <see cref="T:System.Drawing.Image" />.
        /// </returns>
        public override Image Save(Stream stream, Image image)
        {
            // Jpegs can be saved with different settings to include a quality setting for the JPEG compression.
            // This improves output compression and quality. 
            using (EncoderParameters encoderParameters = FormatUtilities.GetEncodingParameters(this.Quality))
            {
                ImageCodecInfo imageCodecInfo =
                    ImageCodecInfo.GetImageEncoders()
                        .FirstOrDefault(ici => ici.MimeType.Equals(this.MimeType, StringComparison.OrdinalIgnoreCase));

                if (imageCodecInfo != null)
                {
                    image.Save(stream, imageCodecInfo, encoderParameters);
                }
            }

            return image;
        }

        /// <summary>
        /// Saves the current image to the specified file path.
        /// </summary>
        /// <param name="path">The path to save the image to.</param>
        /// <param name="image">The 
        /// <see cref="T:System.Drawing.Image" /> to save.</param>
        /// <returns>
        /// The <see cref="T:System.Drawing.Image" />.
        /// </returns>
        public override Image Save(string path, Image image)
        {
            // Jpegs can be saved with different settings to include a quality setting for the JPEG compression.
            // This improves output compression and quality. 
            using (EncoderParameters encoderParameters = FormatUtilities.GetEncodingParameters(this.Quality))
            {
                ImageCodecInfo imageCodecInfo =
                    ImageCodecInfo.GetImageEncoders()
                        .FirstOrDefault(ici => ici.MimeType.Equals(this.MimeType, StringComparison.OrdinalIgnoreCase));

                if (imageCodecInfo != null)
                {
                    image.Save(path, imageCodecInfo, encoderParameters);
                }
            }

            return image;
        }
    }
}