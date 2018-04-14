// Copyright (c) Six Labors and contributors.
// Licensed under the Apache License, Version 2.0.

namespace SixLabors.ImageSharp.Formats.Png
{
    /// <summary>
    /// Contains a list of possible chunk types.
    /// </summary>
    internal enum PngChunkType : uint
    {
        Header = 1229472850U,       // IHDR
        Palette = 1347179589U,      // PLTE
        Data = 1229209940U,         // IDAT
        End = 1229278788U,          // IEND
        PaletteAlpha = 1951551059U, // tRNS
        Text = 1950701684U,         // tEXt
        Gamma = 1732332865U,        // gAMA
        Physical = 1883789683U,     // pHYs
    }
}
