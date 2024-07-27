// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using SixLabors.ImageSharp.Formats.Heif.Av1;

namespace SixLabors.ImageSharp.Tests.Formats.Heif.Av1;

internal class Av1TileDecoderStub : IAv1TileReader
{
    public void ReadTile(Span<byte> tileData, int tileNum)
    {
        // Intentionally left blank.
    }
}
