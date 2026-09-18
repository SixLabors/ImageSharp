// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

namespace SixLabors.ImageSharp.Formats.Jxl.Memory.ImageTypes;

/// <summary>
/// Represents a single-plane, 2D raster image of type <see cref="byte"/>.
/// </summary>
internal sealed class JxlImageB : JxlPlane<byte>
{
    public JxlImageB()
    {
    }

    public JxlImageB(int width, int height)
        : base(width, height)
    {
    }

    public JxlImageB(Configuration configuration, int xSize, int ySize, int prePadding = 0)
        : base(xSize, ySize)
        => this.Allocate(configuration, prePadding);

    public Memory<byte> GetRowBytesMemory(int y)
    {
        DebugGuard.MustBeLessThan(y, this.YSize, nameof(y));

        Memory<byte> row = this.Bytes[(y * this.BytesPerRow)..];

        return row;
    }
}
