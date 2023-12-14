// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

namespace SixLabors.ImageSharp.Formats.Icon.Cur;

/// <summary>
/// IcoFrameMetadata. TODO: Remove base class and merge into this class.
/// </summary>
public class CurFrameMetadata : IconFrameMetadata, IDeepCloneable<CurFrameMetadata>, IDeepCloneable
{
    /// <summary>
    /// Initializes a new instance of the <see cref="CurFrameMetadata"/> class.
    /// </summary>
    public CurFrameMetadata()
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="CurFrameMetadata"/> class.
    /// </summary>
    /// <param name="metadata">metadata</param>
    public CurFrameMetadata(IconFrameMetadata metadata)
        : base(metadata)
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="CurFrameMetadata"/> class.
    /// </summary>
    /// <param name="width">width</param>
    /// <param name="height">height</param>
    /// <param name="colorCount">colorCount</param>
    /// <param name="field1">field1</param>
    /// <param name="field2">field2</param>
    public CurFrameMetadata(byte width, byte height, byte colorCount, ushort field1, ushort field2)
        : base(width, height, colorCount, field1, field2)
    {
    }

    /// <summary>
    /// Gets or sets Specifies the horizontal coordinates of the hotspot in number of pixels from the left.
    /// </summary>
    public ushort HotspotX { get => this.Field1; set => this.Field1 = value; }

    /// <summary>
    /// Gets or sets Specifies the vertical coordinates of the hotspot in number of pixels from the top.
    /// </summary>
    public ushort HotspotY { get => this.Field2; set => this.Field2 = value; }

    /// <inheritdoc/>
    public override CurFrameMetadata DeepClone() => new(this);
}
