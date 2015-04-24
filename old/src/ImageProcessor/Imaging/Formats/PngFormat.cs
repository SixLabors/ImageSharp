// --------------------------------------------------------------------------------------------------------------------
// <copyright file="PngFormat.cs" company="James South">
//   Copyright (c) James South.
//   Licensed under the Apache License, Version 2.0.
// </copyright>
// <summary>
//   Provides the necessary information to support png images.
// </summary>
// --------------------------------------------------------------------------------------------------------------------

namespace ImageProcessor.Imaging.Formats
{
    using System.Drawing;
    using System.Drawing.Imaging;
    using System.IO;

    using ImageProcessor.Imaging.Quantizers;
    using ImageProcessor.Imaging.Quantizers.WuQuantizer;

    /// <summary>
    /// Provides the necessary information to support png images.
    /// </summary>
    public class PngFormat : FormatBase, IQuantizableImageFormat
    {
        /// <summary>
        /// The quantizer for reducing the image palette.
        /// </summary>
        private IQuantizer quantizer = new WuQuantizer();

        /// <summary>
        /// Gets the file headers.
        /// </summary>
        public override byte[][] FileHeaders
        {
            get
            {
                return new[] { new byte[] { 137, 80, 78, 71 } };
            }
        }

        /// <summary>
        /// Gets the list of file extensions.
        /// </summary>
        public override string[] FileExtensions
        {
            get
            {
                return new[] { "png" };
            }
        }

        /// <summary>
        /// Gets the standard identifier used on the Internet to indicate the type of data that a file contains. 
        /// </summary>
        public override string MimeType
        {
            get
            {
                return "image/png";
            }
        }

        /// <summary>
        /// Gets the <see cref="ImageFormat" />.
        /// </summary>
        public override ImageFormat ImageFormat
        {
            get
            {
                return ImageFormat.Png;
            }
        }

        /// <summary>
        /// Gets or sets the quantizer for reducing the image palette.
        /// </summary>
        public IQuantizer Quantizer
        {
            get
            {
                return this.quantizer;
            }

            set
            {
                this.quantizer = value;
            }
        }

        /// <summary>
        /// Gets or sets the color count.
        /// </summary>
        public int ColorCount { get; set; }

        /// <summary>
        /// Saves the current image to the specified output stream.
        /// </summary>
        /// <param name="stream">The <see cref="T:System.IO.Stream" /> to save the image information to.</param>
        /// <param name="image">The <see cref="T:System.Drawing.Image" /> to save.</param>
        /// <returns>
        /// The <see cref="T:System.Drawing.Image" />.
        /// </returns>
        public override Image Save(Stream stream, Image image)
        {
            if (this.IsIndexed)
            {
                image = this.Quantizer.Quantize(image);
            }

            return base.Save(stream, image);
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
            if (this.IsIndexed)
            {
                image = this.Quantizer.Quantize(image);
            }

            return base.Save(path, image);
        }
    }
}