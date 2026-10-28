// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

namespace SixLabors.ImageSharp.Formats.Heif.Av1.Pipeline.TemporalFilter;

/// <summary>
/// Holds the group-of-pictures and rate-control state that the temporal filter reads for one filtered frame.
/// </summary>
/// <remarks>
/// The encoder filters key frames and alternate references while it is positioned at the first frame of the group.
/// Thus some decisions read the frame that the filter changes, and other decisions read the frame that the encoder is positioned at.
/// Each property tells which frame it describes.
/// </remarks>
internal readonly struct Av1TemporalFilterFrameParameters
{
    /// <summary>
    /// Gets the update type of the filtered frame: key frame, alternate reference or intermediate alternate reference.
    /// </summary>
    public Av1FrameUpdateType UpdateType { get; init; }

    /// <summary>
    /// Gets a value indicating whether the filtered frame is a key frame.
    /// </summary>
    public bool IsKeyFrame { get; init; }

    /// <summary>
    /// Gets a value indicating whether the filtered frame is a forward key frame.
    /// </summary>
    public bool IsForwardKeyFrame { get; init; }

    /// <summary>
    /// Gets a value indicating whether the frame the encoder is positioned at is a key frame. In key frame filtering mode one, this
    /// key frame limits the strength of blocks with a large mean difference. This limit also applies when the filter changes the
    /// alternate reference of the group.
    /// </summary>
    public bool CurrentFrameIsKeyFrame { get; init; }

    /// <summary>
    /// Gets a value indicating whether the frame the encoder is positioned at has the key frame update type. This type disables
    /// the row subsampling of the start SAD.
    /// </summary>
    public bool CurrentFrameIsKeyFrameUpdate { get; init; }

    /// <summary>
    /// Gets the number of frames since the last key frame.
    /// </summary>
    public int FramesSinceKey { get; init; }

    /// <summary>
    /// Gets the number of frames to the next key frame.
    /// </summary>
    public int FramesToKey { get; init; }

    /// <summary>
    /// Gets the quantizer index that sets the filter strength. In constant quality mode it is the configured quality level.
    /// Otherwise it is the average quantizer index of the frame type.
    /// </summary>
    public int FilterQIndex { get; init; }

    /// <summary>
    /// Gets a value indicating whether eighth-sample motion is allowed, as the encoder holds this value when the filter runs.
    /// </summary>
    public bool AllowHighPrecisionMotion { get; init; }

    /// <summary>
    /// Gets a value indicating whether motion is restricted to whole samples.
    /// </summary>
    public bool ForceIntegerMotion { get; init; }

    /// <summary>
    /// Gets the encoder frame border: the superblock size plus 32 when the encoder does not resize. The look-ahead frames must hold
    /// at least this many edge-extended samples on every side.
    /// </summary>
    public int BorderInPixels { get; init; }
}
