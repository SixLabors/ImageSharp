// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace SixLabors.ImageSharp.Formats.Heif.Av1.Tiling;

/// <summary>
/// Stores the selected luma and chroma palette sizes and colors for one encoder block.
/// </summary>
[StructLayout(LayoutKind.Sequential, Size = StorageSize)]
internal struct Av1EncoderPaletteInfo
{
    /// <summary>
    /// The packed size of two palette sizes and three eight-color planes.
    /// </summary>
    public const int StorageSize = 50;

    private InlineArray2<byte> paletteSizes;
    private InlineArray24<ushort> paletteColors;

    /// <summary>
    /// Gets the writable luma and shared chroma palette sizes.
    /// </summary>
    [UnscopedRef]
    public Span<byte> PaletteSizes => this.paletteSizes;

    /// <summary>
    /// Gets the selected colors for one color plane.
    /// </summary>
    /// <param name="plane">The color plane.</param>
    /// <returns>The selected colors in palette-index order.</returns>
    [UnscopedRef]
    public Span<ushort> GetColors(Av1Plane plane)
    {
        int planeIndex = (int)plane;
        int paletteSize = this.paletteSizes[planeIndex == 0 ? 0 : 1];
        Span<ushort> colors = this.paletteColors;
        return colors.Slice(planeIndex * Av1Constants.PaletteMaxSize, paletteSize);
    }

    /// <summary>
    /// Stores the selected colors for one color plane.
    /// </summary>
    /// <param name="plane">The color plane.</param>
    /// <param name="colors">The colors in palette-index order.</param>
    public void SetColors(Av1Plane plane, ReadOnlySpan<ushort> colors)
    {
        int offset = (int)plane * Av1Constants.PaletteMaxSize;
        Span<ushort> destination = this.paletteColors;
        colors.CopyTo(destination[offset..]);
    }

    /// <summary>
    /// Provides fixed storage for the luma and shared chroma palette sizes.
    /// </summary>
    /// <typeparam name="T">The stored value type.</typeparam>
    [InlineArray(2)]
    private struct InlineArray2<T>
    {
        private T element;
    }

    /// <summary>
    /// Provides fixed storage for all three eight-color palette planes.
    /// </summary>
    /// <typeparam name="T">The stored value type.</typeparam>
    [InlineArray(3 * Av1Constants.PaletteMaxSize)]
    private struct InlineArray24<T>
    {
        private T element;
    }
}
