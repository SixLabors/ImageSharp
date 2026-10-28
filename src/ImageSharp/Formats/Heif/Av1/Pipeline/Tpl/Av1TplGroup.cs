// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

namespace SixLabors.ImageSharp.Formats.Heif.Av1.Pipeline.Tpl;

/// <summary>
/// The golden group entries that the temporal dependency model reads and writes. The arrays keep entries past
/// <see cref="Size"/> for the look-ahead extension frames. The model reads the layer depth of those entries as the arrays
/// hold it (stale values of earlier groups, or zero). The model writes their update type and quantizer. The owner of the
/// group structure must mirror those entries in both directions.
/// </summary>
internal sealed class Av1TplGroup
{
    /// <summary>
    /// Initializes a new instance of the <see cref="Av1TplGroup"/> class.
    /// </summary>
    public Av1TplGroup()
    {
        int capacity = Av1TplModelConstants.MaximumFrameIndex;
        this.UpdateType = new Av1FrameUpdateType[capacity];
        this.IsKeyFrame = new bool[capacity];
        this.LayerDepth = new int[capacity];
        this.ArfSourceOffset = new int[capacity];
        this.CurrentFrameIndex = new int[capacity];
        this.DisplayIndex = new int[capacity];
        this.QIndex = new int[capacity];
        this.IsNonReference = new bool[capacity];
    }

    /// <summary>
    /// Gets or sets the number of frames in the group.
    /// </summary>
    public int Size { get; set; }

    /// <summary>
    /// Gets or sets the largest layer depth of the group.
    /// </summary>
    public int MaximumLayerDepth { get; set; }

    /// <summary>
    /// Gets or sets the largest layer depth that the group can use.
    /// </summary>
    public int MaximumLayerDepthAllowed { get; set; }

    /// <summary>
    /// Gets or sets the group index of the base-layer alternate reference, or -1.
    /// </summary>
    public int ArfIndex { get; set; }

    /// <summary>
    /// Gets the update type of each entry.
    /// </summary>
    public Av1FrameUpdateType[] UpdateType { get; }

    /// <summary>
    /// Gets a value for each entry that tells whether it is a key frame.
    /// </summary>
    public bool[] IsKeyFrame { get; }

    /// <summary>
    /// Gets the layer depth of each entry.
    /// </summary>
    public int[] LayerDepth { get; }

    /// <summary>
    /// Gets the distance from the source of each entry to its coding position.
    /// </summary>
    public int[] ArfSourceOffset { get; }

    /// <summary>
    /// Gets the look-ahead index of the coding position of each entry.
    /// </summary>
    public int[] CurrentFrameIndex { get; }

    /// <summary>
    /// Gets the display index of each entry within the key frame interval.
    /// </summary>
    public int[] DisplayIndex { get; }

    /// <summary>
    /// Gets the quantizer index of each entry. The rate control preload fills it, and the model stores the leaf quantizer it picks.
    /// </summary>
    public int[] QIndex { get; }

    /// <summary>
    /// Gets a value for each entry that tells whether no later frame references it.
    /// </summary>
    public bool[] IsNonReference { get; }

    /// <summary>
    /// Returns whether the model supplies statistics for an entry. Alternate references, golden frames and key frames are eligible.
    /// </summary>
    /// <param name="index">The group index.</param>
    /// <returns><see langword="true"/> when the entry is eligible.</returns>
    public bool IsTplEligible(int index)
        => this.UpdateType[index] is Av1FrameUpdateType.Alternate or Av1FrameUpdateType.Golden or Av1FrameUpdateType.Key;
}
