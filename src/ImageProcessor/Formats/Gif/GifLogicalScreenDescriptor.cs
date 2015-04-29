// --------------------------------------------------------------------------------------------------------------------
// <copyright file="GifLogicalScreenDescriptor.cs" company="James South">
//   Copyright © James South and contributors.
//   Licensed under the Apache License, Version 2.0.
// </copyright>
// <summary>
//   The Logical Screen Descriptor contains the parameters
//   necessary to define the area of the display device
//   within which the images will be rendered
// </summary>
// --------------------------------------------------------------------------------------------------------------------

namespace ImageProcessor.Formats
{
    /// <summary>
    /// The Logical Screen Descriptor contains the parameters 
    /// necessary to define the area of the display device 
    /// within which the images will be rendered
    /// </summary>
    internal sealed class GifLogicalScreenDescriptor
    {
        /// <summary>
        /// Gets or sets the width, in pixels, of the Logical Screen where the images will 
        /// be rendered in the displaying device.
        /// </summary>
        public short Width { get; set; }

        /// <summary>
        /// Gets or sets the height, in pixels, of the Logical Screen where the images will be 
        /// rendered in the displaying device.
        /// </summary>
        public short Height { get; set; }

        /// <summary>
        /// Gets or sets the index at the Global Color Table for the Background Color. 
        /// The Background Color is the color used for those 
        /// pixels on the screen that are not covered by an image.
        /// </summary>
        public byte BackgroundColorIndex { get; set; }

        /// <summary>
        /// Gets or sets the pixel aspect ratio. Default to 0.
        /// </summary>
        public byte PixelAspectRatio { get; set; }

        /// <summary>
        /// Gets or sets a value indicating whether a flag denoting the presence of a Global Color Table
        /// should be set. 
        /// If the flag is set, the Global Color Table will immediately 
        /// follow the Logical Screen Descriptor.
        /// </summary>
        public bool GlobalColorTableFlag { get; set; }

        /// <summary>
        /// Gets or sets the global color table size.
        /// If the Global Color Table Flag is set to 1, 
        /// the value in this field is used to calculate the number of 
        /// bytes contained in the Global Color Table.
        /// </summary>
        public int GlobalColorTableSize { get; set; }
    }
}
