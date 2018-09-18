// Copyright (c) Six Labors and contributors.
// Licensed under the Apache License, Version 2.0.

namespace SixLabors.ImageSharp.Formats.Png
{
    /// <summary>
    /// Contains a list of chunk types.
    /// </summary>
    internal enum PngChunkType : uint
    {
        /// <summary>
        /// The IDAT chunk contains the actual image data. The image can contains more
        /// than one chunk of this type. All chunks together are the whole image.
        /// </summary>
        Data = 0x49444154U,

        /// <summary>
        /// This chunk must appear last. It marks the end of the PNG data stream.
        /// The chunk's data field is empty.
        /// </summary>
        End = 0x49454E44U,

        /// <summary>
        /// The first chunk in a png file. Can only exists once. Contains
        /// common information like the width and the height of the image or
        /// the used compression method.
        /// </summary>
        Header = 0x49484452U,

        /// <summary>
        /// The PLTE chunk contains from 1 to 256 palette entries, each a three byte
        /// series in the RGB format.
        /// </summary>
        Palette = 0x504C5445U,

        /// <summary>
        /// The eXIf data chunk which contains the Exif profile.
        /// </summary>
        Exif = 0x65584966U,

        /// <summary>
        /// This chunk specifies the relationship between the image samples and the desired
        /// display output intensity.
        /// </summary>
        Gamma = 0x67414D41U,

        /// <summary>
        /// The pHYs chunk specifies the intended pixel size or aspect ratio for display of the image.
        /// </summary>
        Physical = 0x70485973U,

        /// <summary>
        /// Textual information that the encoder wishes to record with the image can be stored in
        /// tEXt chunks. Each tEXt chunk contains a keyword and a text string.
        /// </summary>
        Text = 0x74455874U,

        /// <summary>
        /// This chunk specifies that the image uses simple transparency:
        /// either alpha values associated with palette entries (for indexed-color images)
        /// or a single transparent color (for grayscale and true color images).
        /// </summary>
        PaletteAlpha = 0x74524E53U
    }
}