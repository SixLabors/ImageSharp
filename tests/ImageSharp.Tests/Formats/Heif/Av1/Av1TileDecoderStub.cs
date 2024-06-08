// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using SixLabors.ImageSharp.Formats.Heif.Av1;

namespace SixLabors.ImageSharp.Tests.Formats.Heif.Av1;

internal class Av1TileDecoderStub : IAv1TileDecoder
{
    public void StartDecodeTiles()
    {
    }

    public void DecodeTile(Span<byte> tileData, int tileNum)
    {
    }

    public void FinishDecodeTiles(bool doCdef, bool doLoopRestoration)
    {
    }
}
