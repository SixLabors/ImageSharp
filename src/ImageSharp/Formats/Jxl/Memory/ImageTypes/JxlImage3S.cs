// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

namespace SixLabors.ImageSharp.Formats.Jxl.Memory.ImageTypes;

/// <summary>
/// Represents a three-plane, 2D raster image of type <see cref="short"/>.
/// </summary>
internal sealed class JxlImage3S : JxlImage3<short>
{
    public JxlImage3S()
    {
    }

    public JxlImage3S(Configuration configuration, int xSize, int ySize)
        : base(configuration, xSize, ySize)
    {
    }
}
