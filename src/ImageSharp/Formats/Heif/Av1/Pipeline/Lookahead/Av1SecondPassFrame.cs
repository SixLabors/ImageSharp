// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

namespace SixLabors.ImageSharp.Formats.Heif.Av1.Pipeline.Lookahead;

/// <summary>
/// The frame-level decisions of one coded frame of a look-ahead sequence: its role in the golden frame group, the
/// look-ahead frame it codes, whether it is shown, and the display order it carries.
/// </summary>
internal readonly struct Av1SecondPassFrame
{
    /// <summary>
    /// Gets the index of the frame in its golden frame group.
    /// </summary>
    public int GroupIndex { get; init; }

    /// <summary>
    /// Gets a value indicating whether the frame is the first of a new golden frame group, so that the temporal
    /// filter and the temporal dependency model run for the group before it is coded.
    /// </summary>
    public bool StartsGroup { get; init; }

    /// <summary>
    /// Gets the update role of the frame.
    /// </summary>
    public Av1FrameUpdateType UpdateType { get; init; }

    /// <summary>
    /// Gets a value indicating whether the frame is a key frame.
    /// </summary>
    public bool IsKeyFrame { get; init; }

    /// <summary>
    /// Gets a value indicating whether the frame refreshes every reference slot.
    /// </summary>
    public bool ResetsReferences { get; init; }

    /// <summary>
    /// Gets a value indicating whether the frame is displayed.
    /// </summary>
    public bool ShowFrame { get; init; }

    /// <summary>
    /// Gets a value indicating whether a hidden frame can be shown later. The temporal filter clears it for an alternate reference whose overlay
    /// will be coded.
    /// </summary>
    public bool ShowableFrame { get; init; }

    /// <summary>
    /// Gets a value indicating whether the frame repeats the reference slot that holds its display position.
    /// </summary>
    public bool ShowExistingFrame { get; init; }

    /// <summary>
    /// Gets the offset of the source of the frame from the first frame of the look-ahead that is not yet shown.
    /// </summary>
    public int SourceOffset { get; init; }

    /// <summary>
    /// Gets the display position of the frame since the last key frame. This value is also the order hint before the modulo of the order hint bits.
    /// </summary>
    public int DisplayOrder { get; init; }

    /// <summary>
    /// Gets the number of frames shown since the last key frame when the frame begins. The value is taken before a key frame that resets the
    /// references restarts the count. The temporal dependency model runs at that point.
    /// </summary>
    public int FrameNumber { get; init; }

    /// <summary>
    /// Gets the pyramid layer of the frame.
    /// </summary>
    public int LayerDepth { get; init; }

    /// <summary>
    /// Gets the deepest pyramid layer of the group.
    /// </summary>
    public int MaxLayerDepth { get; init; }

    /// <summary>
    /// Gets the pyramid level that ranks the frame for reference mapping.
    /// </summary>
    public int PyramidLevel { get; init; }

    /// <summary>
    /// Gets the boost of the frame.
    /// </summary>
    public int ArfBoost { get; init; }

    /// <summary>
    /// Gets a value indicating whether the first pass classed the source as animation or graphics.
    /// </summary>
    public bool IsGraphicsAnimation { get; init; }

    /// <summary>
    /// Gets the log of the first-pass intra error of the source.
    /// </summary>
    public double MacroblockAverageEnergy { get; init; }

    /// <summary>
    /// Gets the log of the first-pass wavelet energy of the source, or 0 when the look-ahead has no valid energy.
    /// </summary>
    public double FrameAverageHaarEnergy { get; init; }
}
