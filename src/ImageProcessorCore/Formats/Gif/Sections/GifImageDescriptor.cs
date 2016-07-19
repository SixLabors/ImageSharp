// <copyright file="GifImageDescriptor.cs" company="James Jackson-South">
// Copyright (c) James Jackson-South and contributors.
// Licensed under the Apache License, Version 2.0.
// </copyright>

namespace ImageProcessorCore.Formats
{
    /// <summary>
    /// Each image in the Data Stream is composed of an Image Descriptor,
    /// an optional Local Color Table, and the image data.
    /// Each image must fit within the boundaries of the
    /// Logical Screen, as defined in the Logical Screen Descriptor.
    /// </summary>
    internal sealed class GifImageDescriptor
    {
        /// <summary>
        /// Gets or sets the column number, in pixels, of the left edge of the image,
        /// with respect to the left edge of the Logical Screen.
        /// Leftmost column of the Logical Screen is 0.
        /// </summary>
        public short Left { get; set; }

        /// <summary>
        /// Gets or sets the row number, in pixels, of the top edge of the image with
        /// respect to the top edge of the Logical Screen.
        /// Top row of the Logical Screen is 0.
        /// </summary>
        public short Top { get; set; }

        /// <summary>
        /// Gets or sets the width of the image in pixels.
        /// </summary>
        public short Width { get; set; }

        /// <summary>
        /// Gets or sets the height of the image in pixels.
        /// </summary>
        public short Height { get; set; }

        /// <summary>
        /// Gets or sets a value indicating whether the presence of a Local Color Table immediately
        /// follows this Image Descriptor.
        /// </summary>
        public bool LocalColorTableFlag { get; set; }

        /// <summary>
        /// Gets or sets the local color table size.
        /// If the Local Color Table Flag is set to 1, the value in this field
        /// is used to calculate the number of bytes contained in the Local Color Table.
        /// </summary>
        public int LocalColorTableSize { get; set; }

        /// <summary>
        /// Gets or sets a value indicating whether the image is to be interlaced.
        /// An image is interlaced in a four-pass interlace pattern.
        /// </summary>
        public bool InterlaceFlag { get; set; }
    }
}
