// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using SixLabors.ImageSharp.Formats.Heif.Av1.Pipeline;
using SixLabors.ImageSharp.Formats.Heif.Av1.Tiling;

namespace SixLabors.ImageSharp.Tests.Formats.Heif.Av1;

internal class Av1FrameDecoderStub : IAv1FrameDecoder
{
    private readonly List<Av1SuperblockInfo> superblocks = [];

    public void DecodeSuperblock(Point modeInfoPosition, Av1SuperblockInfo superblockInfo, Av1TileInfo tileInfo)
        => this.superblocks.Add(superblockInfo);

    public int SuperblockCount => this.superblocks.Count;
}
