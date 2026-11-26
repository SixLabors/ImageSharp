// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using SixLabors.ImageSharp.Formats.Heif.Av1;

namespace SixLabors.ImageSharp.Tests.Formats.Heif.Av1;

internal class Av1TileDecoderStub : IAv1TileReader, IAv1TileWriter
{
    private readonly Dictionary<int, byte[]> tileDatas = [];

    public string ReadTile(Span<byte> tileData, int tileNum)
    {
        this.tileDatas.Add(tileNum, tileData.ToArray());
        return null;
    }

    /// <inheritdoc/>
    public string CompleteFrame() => null;

    public ReadOnlySpan<byte> GetTileData(int tileNum)
        => this.tileDatas[tileNum];
}
