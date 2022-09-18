// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using SixLabors.ImageSharp.Formats.OpenExr.Compression;

namespace SixLabors.ImageSharp.Formats.OpenExr;

internal class ExrHeaderAttributes
{
    public IList<ExrChannelInfo> Channels { get; set; }

    public ExrCompressionType? Compression { get; set; }

    public ExrBox2i? DataWindow { get; set; }

    public ExrBox2i? DisplayWindow { get; set; }

    public ExrLineOrder? LineOrder { get; set; }

    public float? AspectRatio { get; set; }

    public float? ScreenWindowWidth { get; set; }

    public PointF? ScreenWindowCenter { get; set; }

    public uint? TileXSize { get; set; }

    public uint? TileYSize { get; set; }

    public int? ChunkCount { get; set; }

    public bool IsValid()
    {
        if (!this.Compression.HasValue)
        {
            return false;
        }

        if (!this.DataWindow.HasValue)
        {
            return false;
        }

        if (!this.DisplayWindow.HasValue)
        {
            return false;
        }

        if (!this.LineOrder.HasValue)
        {
            return false;
        }

        if (!this.AspectRatio.HasValue)
        {
            return false;
        }

        if (!this.ScreenWindowWidth.HasValue)
        {
            return false;
        }

        if (!this.ScreenWindowCenter.HasValue)
        {
            return false;
        }

        if (this.Channels is null)
        {
            return false;
        }

        return true;
    }
}
