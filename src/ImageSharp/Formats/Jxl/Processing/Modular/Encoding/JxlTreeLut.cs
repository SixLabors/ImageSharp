// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

namespace SixLabors.ImageSharp.Formats.Jxl.Processing.Modular.Encoding;

internal sealed class JxlTreeLut<T>(bool hasOffsets, bool hasMultipliers)
{
    public T[] ContextLookup { get; } = new T[2 * JxlMaConstants.PropertyRangeFast];

    public byte[] Offsets { get; } = new byte[hasOffsets ? (2 * JxlMaConstants.PropertyRangeFast) : 0];

    public byte[] Multipliers { get; } = new byte[hasMultipliers ? (2 * JxlMaConstants.PropertyRangeFast) : 0];
}
