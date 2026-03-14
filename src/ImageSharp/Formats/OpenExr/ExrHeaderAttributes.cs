// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using SixLabors.ImageSharp.Formats.OpenExr.Compression;

namespace SixLabors.ImageSharp.Formats.OpenExr;

internal class ExrHeaderAttributes
{
    public ExrHeaderAttributes(
        IList<ExrChannelInfo> channels,
        ExrCompressionType compression,
        ExrBox2i dataWindow,
        ExrBox2i displayWindow,
        ExrLineOrder lineOrder,
        float aspectRatio,
        float screenWindowWidth,
        PointF screenWindowCenter,
        uint? tileXSize = null,
        uint? tileYSize = null,
        int? chunkCount = null)
    {
        this.Channels = channels;
        this.Compression = compression;
        this.DataWindow = dataWindow;
        this.DisplayWindow = displayWindow;
        this.LineOrder = lineOrder;
        this.AspectRatio = aspectRatio;
        this.ScreenWindowWidth = screenWindowWidth;
        this.ScreenWindowCenter = screenWindowCenter;
        this.TileXSize = tileXSize;
        this.TileYSize = tileYSize;
        this.ChunkCount = chunkCount;
    }

    public IList<ExrChannelInfo> Channels { get; set; }

    public ExrCompressionType Compression { get; set; }

    public ExrBox2i DataWindow { get; set; }

    public ExrBox2i DisplayWindow { get; set; }

    public ExrLineOrder LineOrder { get; set; }

    public float AspectRatio { get; set; }

    public float ScreenWindowWidth { get; set; }

    public PointF ScreenWindowCenter { get; set; }

    public uint? TileXSize { get; set; }

    public uint? TileYSize { get; set; }

    public int? ChunkCount { get; set; }
}
