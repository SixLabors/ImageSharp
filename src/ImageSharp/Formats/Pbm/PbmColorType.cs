// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

namespace SixLabors.ImageSharp.Formats.Pbm
{
    /// <summary>
    /// Provides enumeration of available PBM color types.
    /// </summary>
    public enum PbmColorType : byte
    {
        /// <summary>
        /// PBM
        /// </summary>
        BlackAndWhite = 0,

        /// <summary>
        /// PGM - Greyscale. Single component.
        /// </summary>
        Grayscale = 1,

        /// <summary>
        /// PPM - RGB Color. 3 components.
        /// </summary>
        Rgb = 2,
    }
}
