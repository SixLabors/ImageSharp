// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

namespace SixLabors.ImageSharp.Formats;

/// <summary>
/// Defines the contract for all image encoders that allow encoding animation sequences.
/// </summary>
public interface IAnimatedImageEncoder
{
    /// <summary>
    /// Gets the default background color of the canvas when animating in supported encoders.
    /// This color may be used to fill the unused space on the canvas around the frames,
    /// as well as the transparent pixels of the first frame.
    /// The background color is also used when a frame disposal mode is <see cref="FrameDisposalMode.RestoreToBackground"/>.
    /// </summary>
    Color? BackgroundColor { get; }

    /// <summary>
    /// Gets the number of times any animation is repeated in supported encoders.
    /// </summary>
    ushort? RepeatCount { get; }

    /// <summary>
    /// Gets a value indicating whether the root frame is shown as part of the animated sequence in supported encoders.
    /// </summary>
    bool? AnimateRootFrame { get; }
}

/// <summary>
/// Acts as a base class for all image encoders that allow encoding animation sequences.
/// </summary>
public abstract class AnimatedImageEncoder : AlphaAwareImageEncoder, IAnimatedImageEncoder
{
    /// <inheritdoc/>
    public Color? BackgroundColor { get; init; }

    /// <inheritdoc/>
    public ushort? RepeatCount { get; init; }

    /// <inheritdoc/>
    public bool? AnimateRootFrame { get; init; } = true;
}
