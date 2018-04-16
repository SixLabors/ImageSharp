// Copyright (c) Six Labors and contributors.
// Licensed under the Apache License, Version 2.0.

namespace SixLabors.ImageSharp.Formats.Png
{
    /// <summary>
    /// Contains a list of of chunk types.
    /// </summary>
    internal enum PngChunkType : uint
    {
        /// <summary>
        /// The first chunk in a png file. Can only exists once. Contains
        /// common information like the width and the height of the image or
        /// the used compression method.
        /// </summary>
        Header = 1229472850U,       // IHDR

        /// <summary>
        /// The PLTE chunk contains from 1 to 256 palette entries, each a three byte
        /// series in the RGB format.
        /// </summary>
        Palette = 1347179589U,      // PLTE

        /// <summary>
        /// The IDAT chunk contains the actual image data. The image can contains more
        /// than one chunk of this type. All chunks together are the whole image.
        /// </summary>
        Data = 1229209940U,         // IDAT

        /// <summary>
        /// This chunk must appear last. It marks the end of the PNG data stream.
        /// The chunk's data field is empty.
        /// </summary>
        End = 1229278788U,          // IEND

        /// <summary>
        /// This chunk specifies that the image uses simple transparency:
        /// either alpha values associated with palette entries (for indexed-color images)
        /// or a single transparent color (for grayscale and true color images).
        /// </summary>
        PaletteAlpha = 1951551059U, // tRNS

        /// <summary>
        /// Textual information that the encoder wishes to record with the image can be stored in
        /// tEXt chunks. Each tEXt chunk contains a keyword and a text string.
        /// </summary>
        Text = 1950701684U,         // tEXt

        /// <summary>
        /// This chunk specifies the relationship between the image samples and the desired
        /// display output intensity.
        /// </summary>
        Gamma = 1732332865U,        // gAMA

        /// <summary>
        /// The pHYs chunk specifies the intended pixel size or aspect ratio for display of the image.
        /// </summary>
        Physical = 1883789683U,     // pHYs
    }
}
