// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using SixLabors.ImageSharp.Formats.Jxl.Processing.AcStrategy;
using SixLabors.ImageSharp.Formats.Jxl.Processing.Dct;

namespace SixLabors.ImageSharp.Formats.Jxl.Processing.Decoder.Group.BlockLoader;

/// <summary>
/// Interface for reading groups for group decoder.
/// </summary>
internal interface IJxlGetBlock
{
    /// <summary>
    /// Signals the start of a row.
    /// </summary>
    /// <param name="by">Y coordinate of the block.</param>
    public void StartRow(int by);

    /// <summary>
    /// Tries to load a block.
    /// </summary>
    /// <param name="bx">X coordinate of the block.</param>
    /// <param name="by">Y coordinate of the block.</param>
    /// <param name="acs">AC strategy.</param>
    /// <param name="size">Block size.</param>
    /// <param name="log2CoveredBlocks">Log2(N) where N=number of covered blocks.</param>
    /// <param name="block0">First block</param>
    /// <param name="block1">Second block</param>
    /// <param name="block2">Third block</param>
    /// <param name="acType">AC type</param>
    /// <returns>Status of the operation.</returns>
    public bool TryLoadBlock(int bx, int by, JxlAcStrategy acs, int size, int log2CoveredBlocks, JxlDctAcPointer block0, JxlDctAcPointer block1, JxlDctAcPointer block2, JxlDctAcType acType);
}
