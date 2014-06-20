// --------------------------------------------------------------------------------------------------------------------
// <copyright file="BitmapFormat.cs" company="James South">
//   Copyright (c) James South.
//   Licensed under the Apache License, Version 2.0.
// </copyright>
// <summary>
//   Provides the necessary information to support bitmap images.
// </summary>
// --------------------------------------------------------------------------------------------------------------------

namespace ImageProcessor.Imaging.Formats
{
    using System.Drawing.Imaging;
    using System.Text;

    /// <summary>
    /// Provides the necessary information to support bitmap images.
    /// </summary>
    public class BitmapFormat : FormatBase
    {
        /// <summary>
        /// Gets the file headers.
        /// </summary>
        public override byte[][] FileHeaders
        {
            get
            {
                return new[] { Encoding.ASCII.GetBytes("BM") };
            }
        }

        /// <summary>
        /// Gets the list of file extensions.
        /// </summary>
        public override string[] FileExtensions
        {
            get
            {
                return new[] { "bmp" };
            }
        }

        /// <summary>
        /// Gets the standard identifier used on the Internet to indicate the type of data that a file contains. 
        /// </summary>
        public override string MimeType
        {
            get
            {
                return "image/bmp";
            }
        }

        /// <summary>
        /// Gets the <see cref="ImageFormat" />.
        /// </summary>
        public override ImageFormat ImageFormat
        {
            get
            {
                return ImageFormat.Bmp;
            }
        }
    }
}