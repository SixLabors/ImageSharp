// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

namespace SixLabors.ImageSharp.Formats.Jxl.Memory.ImageTypes;

/// <summary>
/// Represents a single-plane, 2D raster image of type <see cref="ushort"/>.
/// </summary>
internal sealed class JxlImageU : JxlPlane<ushort>
{
    public JxlImageU()
    {
    }

    public JxlImageU(int width, int height)
        : base(width, height)
    {
    }

    public JxlImageU(Configuration configuration, int xSize, int ySize, int prePadding = 0)
        : base(xSize, ySize)
        => this.Allocate(configuration, prePadding);
}
