// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

namespace SixLabors.ImageSharp.Formats.Heif.Av1.Tiling;

/// <summary>
/// Provides tile-writer operations that maintain encoder entropy-neighbor state.
/// </summary>
internal partial class Av1TileWriter
{
    /// <summary>
    /// Tracks the mode and coefficient positions while entropy-coding one AV1 superblock.
    /// </summary>
    internal class Av1EntropyCodingContext
    {
        /// <summary>
        /// Gets or sets the macroblock mode information currently being encoded.
        /// </summary>
        public required Av1MacroBlockModeInfo MacroBlockModeInfo { get; set; }

        /// <summary>
        /// Gets or sets the pixel origin of the current superblock.
        /// </summary>
        public Point SuperblockOrigin { get; set; }

        /// <summary>
        /// Gets or sets the number of luma coefficient positions consumed in the current superblock.
        /// </summary>
        public int CodedAreaSuperblock { get; set; }

        /// <summary>
        /// Gets or sets the number of chroma coefficient positions consumed in the current superblock.
        /// </summary>
        public int CodedAreaSuperblockUv { get; set; }
    }
}
