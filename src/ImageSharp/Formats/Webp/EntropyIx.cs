// Copyright (c) Six Labors.
// Licensed under the Apache License, Version 2.0.

namespace SixLabors.ImageSharp.Formats.Webp
{
    /// <summary>
    /// These five modes are evaluated and their respective entropy is computed.
    /// </summary>
    internal enum EntropyIx
    {
        Direct = 0,

        Spatial = 1,

        SubGreen = 2,

        SpatialSubGreen = 3,

        Palette = 4,

        PaletteAndSpatial = 5,

        NumEntropyIx = 6
    }
}
