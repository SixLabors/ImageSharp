// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

namespace SixLabors.ImageSharp.Formats;

internal class AnimatedImageMetadata
{
    /// <summary>
    /// Gets or sets the shared color table.
    /// </summary>
    public ReadOnlyMemory<Color>? ColorTable { get; set; }

    /// <summary>
    /// Gets or sets the shared color table mode.
    /// </summary>
    public FrameColorTableMode ColorTableMode { get; set; }

    /// <summary>
    /// Gets or sets the default background color of the canvas when animating.
    /// This color may be used to fill the unused space on the canvas around the frames,
    /// as well as the transparent pixels of the first frame.
    /// The background color is also used when the disposal mode is <see cref="FrameDisposalMode.RestoreToBackground"/>.
    /// </summary>
    public Color BackgroundColor { get; set; }

    /// <summary>
    /// Gets or sets the number of times any animation is repeated.
    /// <remarks>
    /// 0 means to repeat indefinitely, count is set as repeat n-1 times. Defaults to 1.
    /// </remarks>
    /// </summary>
    public ushort RepeatCount { get; set; }
}
