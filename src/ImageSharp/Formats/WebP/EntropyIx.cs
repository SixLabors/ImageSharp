// Copyright (c) Six Labors and contributors.
// Licensed under the GNU Affero General Public License, Version 3.

namespace SixLabors.ImageSharp.Formats.WebP
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

        NumEntropyIx = 5
    }
}
