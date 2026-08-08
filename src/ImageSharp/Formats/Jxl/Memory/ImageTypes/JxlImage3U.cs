// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

namespace SixLabors.ImageSharp.Formats.Jxl.Memory.ImageTypes;

/// <summary>
/// Represents a three-plane, 2D raster image of type <see cref="ushort"/>.
/// </summary>
internal sealed class JxlImage3U : JxlImage3<ushort>
{
    public JxlImage3U()
    {
    }

    public JxlImage3U(Configuration configuration, int xSize, int ySize)
        : base(configuration, xSize, ySize)
    {
    }
}
