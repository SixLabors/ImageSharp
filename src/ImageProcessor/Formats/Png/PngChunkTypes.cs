// --------------------------------------------------------------------------------------------------------------------
// <copyright file="PngChunkTypes.cs" company="James South">
//   Copyright (c) James South and contributors.
//   Licensed under the Apache License, Version 2.0.
// </copyright>
// <summary>
//   Contains a list of possible chunk type identifiers.
// </summary>
// --------------------------------------------------------------------------------------------------------------------

namespace ImageProcessor.Formats
{
    /// <summary>
    /// Contains a list of possible chunk type identifiers.
    /// </summary>
    internal static class PngChunkTypes
    {
        /// <summary>
        /// The first chunk in a png file. Can only exists once. Contains 
        /// common information like the width and the height of the image or
        /// the used compression method.
        /// </summary>
        public const string Header = "IHDR";

        /// <summary>
        /// The PLTE chunk contains from 1 to 256 palette entries, each a three byte
        /// series in the RGB format.
        /// </summary>
        public const string Palette = "PLTE";

        /// <summary>
        /// The IDAT chunk contains the actual image data. The image can contains more
        /// than one chunk of this type. All chunks together are the whole image.
        /// </summary>
        public const string Data = "IDAT";

        /// <summary>
        /// This chunk must appear last. It marks the end of the PNG data stream. 
        /// The chunk's data field is empty. 
        /// </summary>
        public const string End = "IEND";

        /// <summary>
        /// This chunk specifies that the image uses simple transparency: 
        /// either alpha values associated with palette entries (for indexed-color images) 
        /// or a single transparent color (for grayscale and true color images). 
        /// </summary>
        public const string PaletteAlpha = "tRNS";

        /// <summary>
        /// Textual information that the encoder wishes to record with the image can be stored in 
        /// tEXt chunks. Each tEXt chunk contains a keyword and a text string.
        /// </summary>
        public const string Text = "tEXt";

        /// <summary>
        /// This chunk specifies the relationship between the image samples and the desired 
        /// display output intensity.
        /// </summary>
        public const string Gamma = "gAMA";

        /// <summary>
        /// The pHYs chunk specifies the intended pixel size or aspect ratio for display of the image. 
        /// </summary>
        public const string Physical = "pHYs";
    }
}
