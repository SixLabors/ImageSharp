// --------------------------------------------------------------------------------------------------------------------
// <copyright file="PngHeader.cs" company="James South">
//   Copyright © James South and contributors.
//   Licensed under the Apache License, Version 2.0.
// </copyright>
// <summary>
//   Represents the png header chunk.
// </summary>
// --------------------------------------------------------------------------------------------------------------------

namespace ImageProcessor.Formats
{
    /// <summary>
    /// Represents the png header chunk.
    /// </summary>
    public sealed class PngHeader
    {
        /// <summary>
        /// Gets or sets the dimension in x-direction of the image in pixels.
        /// </summary>
        public int Width { get; set; }

        /// <summary>
        /// Gets or sets the dimension in y-direction of the image in pixels.
        /// </summary>
        public int Height { get; set; }

        /// <summary>
        /// Gets or sets the bit depth.
        /// Bit depth is a single-byte integer giving the number of bits per sample 
        /// or per palette index (not per pixel). Valid values are 1, 2, 4, 8, and 16, 
        /// although not all values are allowed for all color types. 
        /// </summary>
        public byte BitDepth { get; set; }

        /// <summary>
        /// Gets or sets the color type.
        /// Color type is a integer that describes the interpretation of the 
        /// image data. Color type codes represent sums of the following values: 
        /// 1 (palette used), 2 (color used), and 4 (alpha channel used).
        /// </summary>
        public byte ColorType { get; set; }

        /// <summary>
        /// Gets or sets the compression method.
        /// Indicates the method used to compress the image data. At present, 
        /// only compression method 0 (deflate/inflate compression with a sliding 
        /// window of at most 32768 bytes) is defined.
        /// </summary>
        public byte CompressionMethod { get; set; }

        /// <summary>
        /// Gets or sets the preprocessing method.
        /// Indicates the preprocessing method applied to the image 
        /// data before compression. At present, only filter method 0 
        /// (adaptive filtering with five basic filter types) is defined.
        /// </summary>
        public byte FilterMethod { get; set; }

        /// <summary>
        /// Gets or sets the  transmission order.
        /// Indicates the transmission order of the image data. 
        /// Two values are currently defined: 0 (no interlace) or 1 (Adam7 interlace).
        /// </summary>
        public byte InterlaceMethod { get; set; }
    }
}
