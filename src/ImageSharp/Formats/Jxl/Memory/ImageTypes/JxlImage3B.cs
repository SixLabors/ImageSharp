// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

namespace SixLabors.ImageSharp.Formats.Jxl.Memory.ImageTypes;

/// <summary>
/// Represents a three-plane, 2D raster image of type <see cref="byte"/>.
/// </summary>
internal sealed class JxlImage3B : JxlImage3<byte>
{
    public JxlImage3B()
    {
    }

    public JxlImage3B(Configuration configuration, int xSize, int ySize)
        : base(configuration, xSize, ySize)
    {
    }
}
