// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using SixLabors.ImageSharp.Formats.Heif.Av1.Entropy;

namespace SixLabors.ImageSharp.Formats.Heif.Av1.Tiling;

/// <content>
/// Defines the final-block decision contract used by interleaved tile encoding.
/// </content>
internal partial class Av1TileWriter
{
    /// <summary>
    /// Supplies a final block decision immediately before its symbols are written.
    /// </summary>
    internal interface IBlockEncodingHandler
    {
        /// <summary>
        /// Encodes one final block against the current reconstructed neighbors and live tile probabilities.
        /// </summary>
        /// <param name="writer">The live tile symbol encoder.</param>
        /// <param name="macroBlock">The current block's mapped neighbor state.</param>
        /// <param name="blockOrigin">The absolute luma-sample origin.</param>
        /// <param name="tileIndex">The zero-based tile index.</param>
        /// <param name="modeInfo">The mode information to publish.</param>
        /// <param name="block">The encoder block state to publish.</param>
        /// <param name="paletteInfo">The current block's palette sizes and colors.</param>
        void EncodeBlock(
            Av1SymbolEncoder writer,
            Av1MacroBlockD macroBlock,
            Point blockOrigin,
            ushort tileIndex,
            ref Av1MacroBlockModeInfo modeInfo,
            ref Av1EncoderBlockStruct block,
            ref Av1EncoderPaletteInfo paletteInfo);
    }

    private readonly struct PrecomputedBlockEncodingHandler : IBlockEncodingHandler
    {
        public void EncodeBlock(
            Av1SymbolEncoder writer,
            Av1MacroBlockD macroBlock,
            Point blockOrigin,
            ushort tileIndex,
            ref Av1MacroBlockModeInfo modeInfo,
            ref Av1EncoderBlockStruct block,
            ref Av1EncoderPaletteInfo paletteInfo)
        {
        }
    }
}
