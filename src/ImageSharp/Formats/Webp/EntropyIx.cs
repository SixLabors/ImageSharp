// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

namespace SixLabors.ImageSharp.Formats.Webp
{
    /// <summary>
    /// These five modes are evaluated and their respective entropy is computed.
    /// </summary>
    internal enum EntropyIx : byte
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
