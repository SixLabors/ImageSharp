// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using SixLabors.ImageSharp.Formats.Exr.Constants;

namespace SixLabors.ImageSharp.Formats.Exr;

/// <summary>
/// The header of an EXR image.
/// <see href="https://openexr.com/en/latest/TechnicalIntroduction.html#header"/>
/// </summary>
internal class ExrHeaderAttributes
{
    public ExrHeaderAttributes(
        IList<ExrChannelInfo> channels,
        ExrCompression compression,
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

    /// <summary>
    /// Gets or sets a description of the image channels stored in the file.
    /// </summary>
    public IList<ExrChannelInfo> Channels { get; set; }

    /// <summary>
    /// Gets or sets the compression method applied to the pixel data of all channels in the file.
    /// </summary>
    public ExrCompression Compression { get; set; }

    /// <summary>
    /// Gets or sets the image’s data window.
    /// </summary>
    public ExrBox2i DataWindow { get; set; }

    /// <summary>
    /// Gets or sets the image’s display window.
    /// </summary>
    public ExrBox2i DisplayWindow { get; set; }

    /// <summary>
    /// Gets or sets in what order the scan lines in the file are stored in the file (increasing Y, decreasing Y, or, for tiled images, also random Y).
    /// </summary>
    public ExrLineOrder LineOrder { get; set; }

    /// <summary>
    /// Gets or sets the aspect ratio of the image.
    /// </summary>
    public float AspectRatio { get; set; }

    /// <summary>
    /// Gets or sets the screen width.
    /// </summary>
    public float ScreenWindowWidth { get; set; }

    /// <summary>
    /// Gets or sets the screen window center.
    /// </summary>
    public PointF ScreenWindowCenter { get; set; }

    /// <summary>
    /// Gets or sets the number of horizontal tiles.
    /// </summary>
    public uint? TileXSize { get; set; }

    /// <summary>
    /// Gets or sets the number of vertical tiles.
    /// </summary>
    public uint? TileYSize { get; set; }

    /// <summary>
    /// Gets or sets the chunk count. Indicates the number of chunks in this part. Required if the multipart bit (12) is set.
    /// </summary>
    public int? ChunkCount { get; set; }
}
