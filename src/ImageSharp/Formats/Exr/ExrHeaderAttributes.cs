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
    /// <summary>
    /// Initializes a new instance of the <see cref="ExrHeaderAttributes" /> class.
    /// </summary>
    /// <param name="channels">The image channels.</param>
    /// <param name="compression">The compression used.</param>
    /// <param name="dataWindow">The data window.</param>
    /// <param name="displayWindow">The display window.</param>
    /// <param name="lineOrder">The line order.</param>
    /// <param name="aspectRatio">The aspect ratio.</param>
    /// <param name="screenWindowWidth">Width of the screen window.</param>
    /// <param name="screenWindowCenter">The screen window center.</param>
    /// <param name="tileXSize">Size of the tile in x dimension.</param>
    /// <param name="tileYSize">Size of the tile in y dimension.</param>
    /// <param name="chunkCount">The chunk count.</param>
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
